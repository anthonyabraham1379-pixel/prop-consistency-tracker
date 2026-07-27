// ===================================================================
//  INSTITUTIONAL ORDER FLOW - NinjaTrader 8 (NinjaScript / C#)
//  Detector de participacion institucional para ES (E-mini S&P 500).
//
//  Construido exclusivamente sobre Order Flow y Auction Market Theory.
//  No usa medias moviles, RSI, MACD ni ningun indicador tradicional.
//
//  ARQUITECTURA MULTI-TIMEFRAME
//    15m  CONTEXTO   perfil de volumen, niveles institucionales, sesgo
//     5m  REFINADO   estructura de liquidez, swings, EQH/EQL, sweeps
//     1m  EJECUCION  footprint, imbalances, absorcion, delta, senal
//  El contexto se construye primero y solo permite entradas en 1m si el
//  nivel superior lo autoriza. Ese embudo es lo que corta las falsas.
//
//  MODULOS (clases separadas, principio de responsabilidad unica)
//    PerfilVolumenSesion    POC, VAH, VAL, HVN, LVN, POC virgen
//    NivelesInstitucionales OR, IB, PDH/PDL, PWH/PWL, Globex, Asia, London
//    DetectorLiquidez       swings, EQH/EQL, pools, sweep verdadero
//    LectorFootprint        bid/ask por nivel, delta, imbalances, absorcion,
//                           agotamiento, atrapados, subasta terminada
//    MotorScore             puntuacion sobre 100 y decision final
//
//  SCORE (100 puntos)
//    Sweep de liquidez        20
//    Zona del Volume Profile  20
//    Absorcion                20
//    Stacked imbalance        15
//    Delta confirmado         15
//    Subasta terminada        10
//  Senal solo con score >= UmbralScore (85 por defecto).
//
//  COMO SE APLICA
//  El indicador va sobre un grafico de 1 MINUTO del ES. Las series de 5m,
//  15m y el footprint las anade el propio codigo; el footprint replica el
//  periodo del grafico para que los indices cuadren 1 a 1. Si lo pones en
//  un grafico de 5m el embudo pierde sentido: la ejecucion ya no es de 1m.
//
//  NOTA SOBRE FRECUENCIA
//  Exigir las ocho condiciones a la vez produce MUY pocas senales. Es el
//  objetivo declarado (calidad sobre cantidad), pero conviene saberlo: con
//  85 esperar del orden de una senal por semana o menos en ES. El umbral
//  es configurable justamente para poder medir el compromiso.
//
//  Detalle importante del score: sweep, zona de perfil, delta, rechazo y
//  contexto de 15m son puertas duras (sin ellas no hay evaluacion). El
//  suelo que dejan es 55 puntos, asi que con umbral 85 hacen falta dos de
//  los tres opcionales, y en la practica la absorcion (20) pasa a ser casi
//  obligatoria. Bajando a 70 basta con uno.
//
//  RENDIMIENTO
//  El escaneo del footprint se hace UNA vez por vela y se cachea en una
//  estructura. El perfil de sesion se acumula de forma incremental y el
//  area de valor solo se recalcula cuando cambia el POC. Sin loops
//  redundantes ni recalculos por tick.
//
//  REQUISITO: barras Volumetric (licencia Lifetime u Order Flow+).
//  ARCHIVO 100% ASCII.
// ===================================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.BarsTypes;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    #region Tipos de dominio

    /// <summary>Direccion de una lectura o de una senal.</summary>
    public enum IofDir { Ninguna = 0, Largo = 1, Corto = -1 }

    /// <summary>Un nivel institucional con su nombre, para saber por que se opero.</summary>
    public class NivelInst
    {
        public string Nombre;
        public double Precio;
        public NivelInst(string n, double p) { Nombre = n; Precio = p; }
    }

    /// <summary>Una bolsa de liquidez detectada (swing, EQH/EQL).</summary>
    public class PoolLiquidez
    {
        public double Precio;
        public bool   EsCompra;      // true = BSL (por encima), false = SSL (por debajo)
        public bool   EsIgual;       // formado por Equal Highs / Equal Lows
        public int    BarraOrigen;
        public bool   Barrido;
    }

    /// <summary>Lectura completa del footprint de UNA vela. Se calcula una sola vez.</summary>
    public class LecturaFootprint
    {
        public bool   Valida;
        public double Poc;            // precio con mas volumen dentro de la vela
        public double PocRel;         // 0 = POC en el minimo, 1 = en el maximo
        public double Delta;
        public double DeltaPct;       // delta / volumen total
        public double VolumenTotal;
        public double MaxSeen, MinSeen;
        public int    ImbCompra, ImbVenta;      // imbalances apiladas maximas
        public double ImbPrecioAlto, ImbPrecioBajo;  // donde quedo la pila
        public bool   SubastaTerminadaArriba;   // sin volumen agresivo en el extremo
        public bool   SubastaTerminadaAbajo;
        public bool   AtrapadosCompradores;     // delta fuerte al alza y cierre abajo
        public bool   AtrapadosVendedores;
        public double VolExtremoAlto, VolExtremoBajo;
    }

    #endregion

    #region Modulo: perfil de volumen de la sesion

    /// <summary>
    /// Perfil de volumen de la sesion construido de forma INCREMENTAL desde
    /// las barras Volumetric. Acumula volumen por nivel de precio y expone
    /// POC, area de valor, HVN y LVN. El area de valor solo se recalcula
    /// cuando cambia el POC, para no gastar CPU en cada vela.
    /// </summary>
    public class PerfilVolumenSesion
    {
        private readonly Dictionary<int, double> perfil = new Dictionary<int, double>();
        private readonly double tick;
        private int    pocIdx = int.MinValue;
        private double pocVol = 0;
        private bool   vaSucia = true;

        public double Poc { get; private set; }
        public double Vah { get; private set; }
        public double Val { get; private set; }
        public double VolumenTotal { get; private set; }
        public double PocAnterior { get; set; }      // POC de la sesion previa
        public bool   PocAnteriorVirgen { get; set; } // aun no revisitado

        public PerfilVolumenSesion(double tickSize) { tick = tickSize; }

        public void Reiniciar()
        {
            perfil.Clear();
            pocIdx = int.MinValue; pocVol = 0; VolumenTotal = 0;
            Poc = Vah = Val = 0; vaSucia = true;
        }

        /// <summary>Suma el volumen de un nivel de precio. O(1).</summary>
        public void Anadir(double precio, double vol)
        {
            if (vol <= 0) return;
            int idx = (int)Math.Round(precio / tick);
            double acum;
            perfil.TryGetValue(idx, out acum);
            acum += vol;
            perfil[idx] = acum;
            VolumenTotal += vol;
            if (acum > pocVol) { pocVol = acum; pocIdx = idx; Poc = idx * tick; vaSucia = true; }
        }

        /// <summary>
        /// Area de valor por el metodo estandar: se parte del POC y se van
        /// anexando los niveles vecinos mas voluminosos hasta cubrir el 70%.
        /// Solo se ejecuta si el POC cambio (cache).
        /// </summary>
        public void RecalcularAreaValor(double porcentaje)
        {
            if (!vaSucia || pocIdx == int.MinValue || perfil.Count == 0) return;
            vaSucia = false;
            double objetivo = VolumenTotal * porcentaje;
            double acum = pocVol;
            int arriba = pocIdx, abajo = pocIdx;
            int guardia = 0, maxIter = perfil.Count * 2 + 10;
            while (acum < objetivo && guardia++ < maxIter)
            {
                double vArriba = Vol(arriba + 1);
                double vAbajo  = Vol(abajo - 1);
                if (vArriba <= 0 && vAbajo <= 0) break;
                if (vArriba >= vAbajo) { arriba++; acum += vArriba; }
                else                   { abajo--;  acum += vAbajo;  }
            }
            Vah = arriba * tick;
            Val = abajo  * tick;
        }

        private double Vol(int idx)
        {
            double v; return perfil.TryGetValue(idx, out v) ? v : 0;
        }

        /// <summary>Volumen relativo de un precio respecto al POC. 1 = tan negociado como el POC.</summary>
        public double VolumenRelativo(double precio)
        {
            if (pocVol <= 0) return 0;
            return Vol((int)Math.Round(precio / tick)) / pocVol;
        }

        /// <summary>Nodo de ALTO volumen: mucho tiempo aceptado ahi.</summary>
        public bool EsHvn(double precio, double umbral) { return VolumenRelativo(precio) >= umbral; }

        /// <summary>Nodo de BAJO volumen: el precio lo rechazo, suele recorrerse rapido.</summary>
        public bool EsLvn(double precio, double umbral)
        {
            double r = VolumenRelativo(precio);
            return r > 0 && r <= umbral;
        }
    }

    #endregion

    #region Modulo: niveles institucionales

    /// <summary>
    /// Todos los niveles de referencia que la institucion vigila. Se
    /// actualizan de forma incremental y se congelan cuando su ventana
    /// termina: ninguno se recalcula hacia atras.
    /// </summary>
    public class NivelesInstitucionales
    {
        public double OrHigh = double.NaN, OrLow = double.NaN;      // Opening Range
        public double IbHigh = double.NaN, IbLow = double.NaN;      // Initial Balance
        public double Pdh = double.NaN, Pdl = double.NaN;           // dia previo
        public double Pwh = double.NaN, Pwl = double.NaN;           // semana previa
        public double GlobexHigh = double.NaN, GlobexLow = double.NaN;
        public double AsiaHigh = double.NaN, AsiaLow = double.NaN;
        public double LondonHigh = double.NaN, LondonLow = double.NaN;

        private double dHigh = double.NaN, dLow = double.NaN;       // dia en curso
        private double wHigh = double.NaN, wLow = double.NaN;       // semana en curso

        public void NuevoDia()
        {
            Pdh = dHigh; Pdl = dLow;
            dHigh = dLow = double.NaN;
            OrHigh = OrLow = IbHigh = IbLow = double.NaN;
            GlobexHigh = GlobexLow = AsiaHigh = AsiaLow = double.NaN;
            LondonHigh = LondonLow = double.NaN;
        }

        public void NuevaSemana()
        {
            Pwh = wHigh; Pwl = wLow;
            wHigh = wLow = double.NaN;
        }

        private static void Ext(ref double hi, ref double lo, double h, double l)
        {
            hi = double.IsNaN(hi) ? h : Math.Max(hi, h);
            lo = double.IsNaN(lo) ? l : Math.Min(lo, l);
        }

        /// <summary>Alimenta todos los rangos segun la hora ET de la vela.</summary>
        public void Alimentar(double h, double l, int hm, int orMin, int ibMin)
        {
            Ext(ref dHigh, ref dLow, h, l);
            Ext(ref wHigh, ref wLow, h, l);

            // Globex: 18:00 -> 09:30 ET
            if (hm >= 1800 || hm < 930) Ext(ref GlobexHigh, ref GlobexLow, h, l);
            // Asia: 19:00 -> 04:00 ET
            if (hm >= 1900 || hm < 400) Ext(ref AsiaHigh, ref AsiaLow, h, l);
            // Londres: 03:00 -> 08:30 ET
            if (hm >= 300 && hm < 830) Ext(ref LondonHigh, ref LondonLow, h, l);

            // Opening Range e Initial Balance desde las 09:30
            int desdeApertura = MinutosDesde(hm, 930);
            if (desdeApertura >= 0 && desdeApertura < orMin) Ext(ref OrHigh, ref OrLow, h, l);
            if (desdeApertura >= 0 && desdeApertura < ibMin) Ext(ref IbHigh, ref IbLow, h, l);
        }

        private static int MinutosDesde(int hm, int refHm)
        {
            int a = (hm / 100) * 60 + hm % 100;
            int b = (refHm / 100) * 60 + refHm % 100;
            return a - b;
        }

        /// <summary>Devuelve el nivel institucional mas cercano dentro de la tolerancia.</summary>
        public NivelInst MasCercano(double precio, double tolerancia)
        {
            NivelInst mejor = null;
            double dMin = double.MaxValue;
            Action<string, double> probar = (nombre, nivel) =>
            {
                if (double.IsNaN(nivel) || nivel <= 0) return;
                double d = Math.Abs(precio - nivel);
                if (d <= tolerancia && d < dMin) { dMin = d; mejor = new NivelInst(nombre, nivel); }
            };
            probar("PDH", Pdh); probar("PDL", Pdl);
            probar("PWH", Pwh); probar("PWL", Pwl);
            probar("GlobexH", GlobexHigh); probar("GlobexL", GlobexLow);
            probar("AsiaH", AsiaHigh);     probar("AsiaL", AsiaLow);
            probar("LondonH", LondonHigh); probar("LondonL", LondonLow);
            probar("ORH", OrHigh);         probar("ORL", OrLow);
            probar("IBH", IbHigh);         probar("IBL", IbLow);
            return mejor;
        }
    }

    #endregion

    #region Modulo: deteccion de liquidez

    /// <summary>
    /// Swings, Equal Highs/Lows, pools y sweeps. Un sweep VERDADERO exige
    /// las cuatro cosas: ruptura temporal, rechazo inmediato, regreso dentro
    /// del rango y aumento de volumen. Sin eso es una ruptura normal.
    /// </summary>
    public class DetectorLiquidez
    {
        private readonly List<PoolLiquidez> pools = new List<PoolLiquidez>();
        public IReadOnlyList<PoolLiquidez> Pools { get { return pools; } }

        public double UltimoSwingAlto = double.NaN, PrevSwingAlto = double.NaN;
        public double UltimoSwingBajo = double.NaN, PrevSwingBajo = double.NaN;
        public bool   EqualHighs, EqualLows;

        public void RegistrarSwingAlto(double p, int barra, double tolIgual)
        {
            PrevSwingAlto = UltimoSwingAlto;
            UltimoSwingAlto = p;
            EqualHighs = !double.IsNaN(PrevSwingAlto) && Math.Abs(p - PrevSwingAlto) <= tolIgual;
            pools.Add(new PoolLiquidez { Precio = p, EsCompra = true, EsIgual = EqualHighs, BarraOrigen = barra });
            Podar();
        }

        public void RegistrarSwingBajo(double p, int barra, double tolIgual)
        {
            PrevSwingBajo = UltimoSwingBajo;
            UltimoSwingBajo = p;
            EqualLows = !double.IsNaN(PrevSwingBajo) && Math.Abs(p - PrevSwingBajo) <= tolIgual;
            pools.Add(new PoolLiquidez { Precio = p, EsCompra = false, EsIgual = EqualLows, BarraOrigen = barra });
            Podar();
        }

        private void Podar()
        {
            // memoria acotada: solo importan las bolsas recientes sin barrer
            while (pools.Count > 60) pools.RemoveAt(0);
        }

        /// <summary>
        /// Un sweep valido es ruptura + rechazo + regreso + volumen.
        /// Devuelve la bolsa barrida, o null si fue una ruptura normal.
        /// </summary>
        public PoolLiquidez EvaluarSweep(double high, double low, double open, double close,
                                          double volRatio, double mechaMin, double volMin,
                                          bool buscarCompra)
        {
            double rango = Math.Max(high - low, 1e-9);
            for (int i = pools.Count - 1; i >= 0; i--)
            {
                PoolLiquidez p = pools[i];
                if (p.Barrido || p.EsCompra != buscarCompra) continue;

                if (buscarCompra)
                {
                    bool ruptura = high > p.Precio;              // salio del rango
                    bool regreso = close < p.Precio;             // y cerro dentro otra vez
                    double mecha = (high - Math.Max(open, close)) / rango;
                    if (ruptura && regreso && mecha >= mechaMin && volRatio >= volMin)
                    { p.Barrido = true; return p; }
                }
                else
                {
                    bool ruptura = low < p.Precio;
                    bool regreso = close > p.Precio;
                    double mecha = (Math.Min(open, close) - low) / rango;
                    if (ruptura && regreso && mecha >= mechaMin && volRatio >= volMin)
                    { p.Barrido = true; return p; }
                }
            }
            return null;
        }
    }

    #endregion

    public class InstitutionalOrderFlow : Indicator
    {
        // ---------- indices de series ----------
        private int Idx5 = 1;    // refinado
        private int Idx15 = 2;   // contexto
        private int IdxVol = 3;  // footprint (Volumetric, misma TF que la primaria)

        // ---------- modulos ----------
        private PerfilVolumenSesion    perfil;
        private NivelesInstitucionales niveles;
        private DetectorLiquidez       liq5;      // liquidez refinada en 5m

        // ---------- cache del footprint ----------
        private LecturaFootprint fp = new LecturaFootprint();

        // ---------- delta acumulado (CVD de la sesion) ----------
        private double cvdSesion = 0;
        private readonly List<double> cvdHist = new List<double>();
        private int cvdBarraInicioSesion = 0;

        // ---------- estado ----------
        private int      semanaActual = -1;
        private PoolLiquidez sweepReciente = null;
        private int      sweepBarra = -999;
        private double   volMedia = 0;
        private int      ultimaSenalBarra = -999;

        // ---------- absorcion ----------
        private double absAcumDelta = 0;
        private int    absBarras = 0;
        private double absPrecioRef = double.NaN;

        // ---------- plots ----------
        private Series<double> serPoc, serVah, serVal;

        // ===============================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Detector de participacion institucional: Volume Profile, liquidez, "
                            + "footprint, imbalances apiladas, absorcion y subasta. Solo Order Flow.";
                Name        = "InstitutionalOrderFlow";
                Calculate   = Calculate.OnBarClose;
                IsOverlay   = true;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;

                // --- 1) Multi-timeframe ---
                MinutosContexto = 15;
                MinutosRefinado = 5;

                // --- 2) Volume Profile ---
                PorcentajeAreaValor = 0.70;
                UmbralHvn           = 0.70;
                UmbralLvn           = 0.25;
                ToleranciaNivelTicks = 8;
                EvitarPoc           = true;
                AnchoPocTicks       = 4;

                // --- 3) Liquidez ---
                PivoteSwing      = 4;
                ToleranciaIgualTicks = 4;
                MechaMinimaSweep = 0.40;
                VolumenMinimoSweep = 1.2;
                VelasValidezSweep  = 6;

                // --- 4) Imbalances ---
                RatioImbalance   = 3.0;
                MinImbalances    = 3;

                // --- 5) Delta ---
                DeltaMinimoPct   = 0.15;
                UsarDivergenciaDelta = true;

                // --- 6) Absorcion ---
                AbsorcionVelas     = 3;
                AbsorcionRangoTicks = 6;
                AbsorcionDeltaMin  = 0.25;

                // --- 7) Score ---
                UmbralScore = 85;
                PtsSweep = 20; PtsPerfil = 20; PtsAbsorcion = 20;
                PtsImbalance = 15; PtsDelta = 15; PtsSubasta = 10;

                // --- 8) Filtros ---
                UsarSesion = true;
                SesionIni  = 930;
                SesionFin  = 1530;
                EvitarLunch = true;
                LunchIni   = 1200;
                LunchFin   = 1330;
                OffsetHorasET = 0;
                VolumenMinimoMedia = 0.8;
                AtrMinimoTicks = 8;
                EvitarBalanceado = true;

                // --- 9) Riesgo mostrado ---
                StopTicksExtra = 4;
                ObjetivoR      = 2.0;

                // --- 10) Visual ---
                MostrarPerfil = true;
                MostrarLiquidez = true;
                MostrarImbalances = true;
                MostrarAbsorcion = true;
                MostrarSenales = true;
                ColorLargo = Brushes.Lime;
                ColorCorto = Brushes.Red;
                ColorPerfil = Brushes.DodgerBlue;

                // --- 11) Alertas ---
                AlertaSonora = true;
                ArchivoSonido = "Alert1.wav";

                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "POC");
                AddPlot(new Stroke(Brushes.DodgerBlue, 1), PlotStyle.Line, "VAH");
                AddPlot(new Stroke(Brushes.DodgerBlue, 1), PlotStyle.Line, "VAL");
            }
            else if (State == State.Configure)
            {
                Idx5   = 1; AddDataSeries(BarsPeriodType.Minute, MinutosRefinado);
                Idx15  = 2; AddDataSeries(BarsPeriodType.Minute, MinutosContexto);
                IdxVol = 3; AddVolumetric(null, BarsPeriod.BarsPeriodType, BarsPeriod.Value,
                                          VolumetricDeltaType.BidAsk, 1);
            }
            else if (State == State.DataLoaded)
            {
                perfil  = new PerfilVolumenSesion(TickSize);
                niveles = new NivelesInstitucionales();
                liq5    = new DetectorLiquidez();
                serPoc  = new Series<double>(this);
                serVah  = new Series<double>(this);
                serVal  = new Series<double>(this);
                Plots[1].Brush = ColorPerfil;
                Plots[2].Brush = ColorPerfil;
            }
        }

        // ===============================================================
        //  BUCLE PRINCIPAL
        // ===============================================================
        protected override void OnBarUpdate()
        {
            // ---- ETAPA 3: footprint de la vela (una sola pasada, cacheado) ----
            if (BarsInProgress == IdxVol) { LeerFootprint(); return; }

            // ---- ETAPA 2: liquidez refinada en 5m ----
            if (BarsInProgress == Idx5) { ActualizarLiquidez5(); return; }

            // ---- ETAPA 1: contexto en 15m ----
            if (BarsInProgress == Idx15) { return; }   // el contexto se lee bajo demanda

            if (BarsInProgress != 0) return;
            if (CurrentBar < 30 || CurrentBars[Idx5] < 10 || CurrentBars[Idx15] < 5) return;
            if (CurrentBars[IdxVol] < 1) return;

            DateTime et = Time[0].AddHours(OffsetHorasET);
            int hm = et.Hour * 60 + et.Minute;
            int hmm = et.Hour * 100 + et.Minute;

            // ---------- corte de sesion / dia / semana ----------
            if (Bars.IsFirstBarOfSession)
            {
                perfil.PocAnterior = perfil.Poc;
                perfil.PocAnteriorVirgen = perfil.Poc > 0;
                perfil.Reiniciar();
                niveles.NuevoDia();
                cvdSesion = 0; cvdHist.Clear(); cvdBarraInicioSesion = CurrentBar;
                int semana = System.Globalization.CultureInfo.InvariantCulture.Calendar
                             .GetWeekOfYear(et, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                if (semana != semanaActual) { niveles.NuevaSemana(); semanaActual = semana; }
            }

            // ---------- ETAPA 1: alimentar perfil y niveles ----------
            AlimentarPerfil();
            niveles.Alimentar(High[0], Low[0], hmm, 30, 60);
            perfil.RecalcularAreaValor(PorcentajeAreaValor);

            serPoc[0] = perfil.Poc; serVah[0] = perfil.Vah; serVal[0] = perfil.Val;
            // NaN = el plot no dibuja nada. Asi el interruptor visual no
            // ensucia el grafico con lineas pegadas al precio.
            Values[0][0] = MostrarPerfil && perfil.Poc > 0 ? perfil.Poc : double.NaN;
            Values[1][0] = MostrarPerfil && perfil.Vah > 0 ? perfil.Vah : double.NaN;
            Values[2][0] = MostrarPerfil && perfil.Val > 0 ? perfil.Val : double.NaN;

            // POC virgen: deja de serlo cuando el precio lo toca
            if (perfil.PocAnteriorVirgen && High[0] >= perfil.PocAnterior && Low[0] <= perfil.PocAnterior)
                perfil.PocAnteriorVirgen = false;

            // ---------- ETAPA 5: delta acumulado ----------
            if (fp.Valida)
            {
                cvdSesion += fp.Delta;
                cvdHist.Add(cvdSesion);
                if (cvdHist.Count > 500) cvdHist.RemoveAt(0);
            }

            // ---------- volumen medio de referencia ----------
            volMedia = volMedia <= 0 ? Volume[0] : volMedia * 0.95 + Volume[0] * 0.05;
            double volRatio = volMedia > 0 ? Volume[0] / volMedia : 1.0;

            // ---------- ETAPA 6: absorcion ----------
            ActualizarAbsorcion();

            // ---------- ETAPA 2: sweep sobre la vela de 1m ----------
            DetectarSweep(volRatio);

            // ---------- capa visual (etapa 10) ----------
            PintarContexto();

            // ---------- FILTROS DE CONTEXTO ----------
            if (!PasaFiltros(hmm, volRatio)) return;

            // ---------- ETAPA 7: confluencia y score ----------
            EvaluarSenal();
        }

        // ===============================================================
        //  ETAPA 3: FOOTPRINT (una pasada por vela, resultado cacheado)
        // ===============================================================
        private void LeerFootprint()
        {
            fp = new LecturaFootprint();
            try
            {
                VolumetricBarsType vb = BarsArray[IdxVol].BarsType as VolumetricBarsType;
                if (vb == null) return;
                var v = vb.Volumes[CurrentBars[IdxVol]];
                if (v == null) return;

                double lo = Lows[IdxVol][0], hi = Highs[IdxVol][0];
                double cl = Closes[IdxVol][0], op = Opens[IdxVol][0];
                if (hi <= lo) return;
                int pasos = (int)Math.Round((hi - lo) / TickSize) + 1;
                if (pasos < 2 || pasos > 2000) return;

                fp.Delta        = v.BarDelta;
                fp.VolumenTotal = Volumes[IdxVol][0];
                fp.DeltaPct     = fp.VolumenTotal > 0 ? fp.Delta / fp.VolumenTotal : 0;
                fp.MaxSeen      = v.MaxSeenDelta;
                fp.MinSeen      = v.MinSeenDelta;

                // --- una sola pasada: POC, imbalances y extremos de subasta ---
                double maxVol = 0, poc = lo;
                int rachaC = 0, rachaV = 0;
                double bidAnterior = 0;
                for (int i = 0; i < pasos; i++)
                {
                    double p   = lo + i * TickSize;
                    double ask = v.GetAskVolumeForPrice(p);
                    double bid = v.GetBidVolumeForPrice(p);
                    double tot = ask + bid;
                    if (tot > maxVol) { maxVol = tot; poc = p; }

                    // ETAPA 4: imbalance en DIAGONAL. El comprador agresivo paga
                    // el ask y el vendedor pega en el bid, asi que no compiten en
                    // el mismo nivel sino en niveles contiguos.
                    if (i > 0)
                    {
                        if (ask > 0 && bidAnterior > 0 && ask >= bidAnterior * RatioImbalance)
                        { rachaC++; if (rachaC > fp.ImbCompra) { fp.ImbCompra = rachaC; fp.ImbPrecioAlto = p; } }
                        else rachaC = 0;
                        if (bidAnterior > 0 && ask > 0 && bidAnterior >= ask * RatioImbalance)
                        { rachaV++; if (rachaV > fp.ImbVenta) { fp.ImbVenta = rachaV; fp.ImbPrecioBajo = p; } }
                        else rachaV = 0;
                    }
                    if (i == 0)          { fp.VolExtremoBajo = tot; fp.SubastaTerminadaAbajo = ask <= 0; }
                    if (i == pasos - 1)  { fp.VolExtremoAlto = tot; fp.SubastaTerminadaArriba = bid <= 0; }
                    bidAnterior = bid;
                }
                if (maxVol <= 0) return;

                fp.Poc    = poc;
                fp.PocRel = (poc - lo) / (hi - lo);

                // Atrapados: el delta empujo fuerte hacia un lado y la vela cerro
                // al otro. Los agresivos que entraron quedaron del lado malo.
                double rango = hi - lo;
                fp.AtrapadosCompradores = fp.DeltaPct > 0.15 && (cl - lo) / rango < 0.35;
                fp.AtrapadosVendedores  = fp.DeltaPct < -0.15 && (hi - cl) / rango < 0.35;

                fp.Valida = true;
            }
            catch (Exception ex)
            {
                Print("Volumetric no disponible (necesitas Lifetime u Order Flow+): " + ex.Message);
            }
        }

        /// <summary>Vuelca el footprint de la vela al perfil de sesion. Incremental.</summary>
        private void AlimentarPerfil()
        {
            try
            {
                VolumetricBarsType vb = BarsArray[IdxVol].BarsType as VolumetricBarsType;
                if (vb == null || CurrentBars[IdxVol] < 0) return;
                var v = vb.Volumes[CurrentBars[IdxVol]];
                if (v == null) return;
                double lo = Lows[IdxVol][0], hi = Highs[IdxVol][0];
                int pasos = (int)Math.Round((hi - lo) / TickSize) + 1;
                if (pasos < 1 || pasos > 2000) return;
                for (int i = 0; i < pasos; i++)
                {
                    double p = lo + i * TickSize;
                    perfil.Anadir(p, v.GetTotalVolumeForPrice(p));
                }
            }
            catch { /* ya avisado en LeerFootprint */ }
        }

        // ===============================================================
        //  ETAPA 2: LIQUIDEZ (swings confirmados en 5m, no repintan)
        // ===============================================================
        private void ActualizarLiquidez5()
        {
            int n = PivoteSwing;
            if (CurrentBars[Idx5] < n * 2 + 1) return;
            double tol = ToleranciaIgualTicks * TickSize;

            bool esAlto = true, esBajo = true;
            double ph = Highs[Idx5][n], pl = Lows[Idx5][n];
            for (int i = 0; i <= n * 2; i++)
            {
                if (i == n) continue;
                if (Highs[Idx5][i] >= ph) esAlto = false;
                if (Lows[Idx5][i]  <= pl) esBajo = false;
            }
            if (esAlto) liq5.RegistrarSwingAlto(ph, CurrentBars[Idx5] - n, tol);
            if (esBajo) liq5.RegistrarSwingBajo(pl, CurrentBars[Idx5] - n, tol);
        }

        private void DetectarSweep(double volRatio)
        {
            PoolLiquidez s = liq5.EvaluarSweep(High[0], Low[0], Open[0], Close[0], volRatio,
                                               MechaMinimaSweep, VolumenMinimoSweep, true);
            if (s == null)
                s = liq5.EvaluarSweep(High[0], Low[0], Open[0], Close[0], volRatio,
                                      MechaMinimaSweep, VolumenMinimoSweep, false);
            if (s != null)
            {
                sweepReciente = s; sweepBarra = CurrentBar;
                if (MostrarLiquidez)
                    Draw.TriangleDown(this, "sw" + CurrentBar, false, 0,
                                      (s.EsCompra ? High[0] : Low[0]) + (s.EsCompra ? 3 : -3) * TickSize,
                                      s.EsCompra ? Brushes.OrangeRed : Brushes.Gold);
            }
        }

        // ===============================================================
        //  ETAPA 6: ABSORCION
        //  El precio deja de avanzar, el volumen sube y el delta sigue
        //  creciendo: alguien esta absorbiendo todo ese flujo agresivo.
        // ===============================================================
        private void ActualizarAbsorcion()
        {
            if (!fp.Valida) { absBarras = 0; absAcumDelta = 0; return; }
            double rangoMax = AbsorcionRangoTicks * TickSize;

            if (double.IsNaN(absPrecioRef)) { absPrecioRef = Close[0]; }
            bool sinAvance = Math.Abs(Close[0] - absPrecioRef) <= rangoMax;

            if (sinAvance)
            {
                absBarras++;
                absAcumDelta += fp.Delta;
            }
            else
            {
                absBarras = 0; absAcumDelta = 0; absPrecioRef = Close[0];
            }
        }

        /// <summary>
        /// ETAPA 5b: CVD de la sesion. Devuelve true si el delta acumulado no
        /// contradice la direccion, o si hay divergencia valida (el precio hizo
        /// nuevo extremo pero el CVD no lo acompano: el movimiento no tiene
        /// agresion detras y suele revertir).
        /// </summary>
        private bool CvdApoya(IofDir dir)
        {
            int n = cvdHist.Count;
            if (n < 20) return true;                 // sin muestra suficiente, no filtramos

            // ventana dentro de la MISMA sesion: comparar CVD entre sesiones
            // no tiene sentido, el acumulado se reinicia cada dia.
            int ventana = Math.Min(n - 1, Math.Min(30, CurrentBar - cvdBarraInicioSesion));
            if (ventana < 10) return true;

            double cvdAtras = cvdHist[n - 1 - ventana];
            double dCvd     = cvdSesion - cvdAtras;

            if (dir == IofDir.Largo)
            {
                bool nuevoMinimo = Low[0] <= MIN(Low, ventana)[1];
                if (nuevoMinimo && dCvd > 0) return true;   // divergencia alcista
                return dCvd > 0 || cvdSesion > 0;
            }
            else
            {
                bool nuevoMaximo = High[0] >= MAX(High, ventana)[1];
                if (nuevoMaximo && dCvd < 0) return true;   // divergencia bajista
                return dCvd < 0 || cvdSesion < 0;
            }
        }

        /// <summary>Absorcion confirmada en la direccion indicada.</summary>
        private bool HayAbsorcion(IofDir dir)
        {
            if (absBarras < AbsorcionVelas || fp.VolumenTotal <= 0) return false;
            double ratio = absAcumDelta / Math.Max(fp.VolumenTotal * absBarras, 1);
            // Para un LARGO queremos que el delta acumulado sea VENDEDOR y el
            // precio no haya cedido: el vendedor esta siendo absorbido.
            return dir == IofDir.Largo ? ratio <= -AbsorcionDeltaMin
                                       : ratio >=  AbsorcionDeltaMin;
        }

        // ===============================================================
        //  ETAPA 10: VISUALIZACION
        //  Solo lo que aporta lectura: imbalances apiladas, zona de
        //  absorcion y bordes del area de valor. Nada decorativo.
        // ===============================================================
        private void PintarContexto()
        {
            if (MostrarImbalances && fp.Valida)
            {
                if (fp.ImbCompra >= MinImbalances)
                    Draw.Dot(this, "ic" + CurrentBar, false, 0,
                             fp.ImbPrecioAlto, Brushes.LimeGreen);
                if (fp.ImbVenta >= MinImbalances)
                    Draw.Dot(this, "iv" + CurrentBar, false, 0,
                             fp.ImbPrecioBajo, Brushes.OrangeRed);
            }

            if (MostrarAbsorcion && absBarras >= AbsorcionVelas)
            {
                // Rectangulo sobre el tramo donde el precio no avanzo pese al
                // flujo agresivo: ahi es donde alguien esta absorbiendo.
                double alto = absPrecioRef + AbsorcionRangoTicks * TickSize;
                double bajo = absPrecioRef - AbsorcionRangoTicks * TickSize;
                Draw.Rectangle(this, "abs" + CurrentBar, false,
                               absBarras, alto, 0, bajo,
                               Brushes.Transparent, Brushes.Goldenrod, 12);
            }
        }

        // ===============================================================
        //  FILTROS
        // ===============================================================
        private bool PasaFiltros(int hmm, double volRatio)
        {
            if (UsarSesion && (hmm < SesionIni || hmm >= SesionFin)) return false;
            if (EvitarLunch && hmm >= LunchIni && hmm < LunchFin) return false;
            if (volRatio < VolumenMinimoMedia) return false;
            if ((High[0] - Low[0]) < AtrMinimoTicks * TickSize) return false;

            // No operar dentro del POC: ahi el mercado esta en equilibrio y no
            // hay desplazamiento que capturar.
            if (EvitarPoc && perfil.Poc > 0
                && Math.Abs(Close[0] - perfil.Poc) <= AnchoPocTicks * TickSize) return false;

            // Mercado balanceado: el precio lleva rato dentro del area de valor.
            if (EvitarBalanceado && perfil.Vah > 0 && perfil.Val > 0)
            {
                bool dentroVA = Close[0] <= perfil.Vah && Close[0] >= perfil.Val;
                bool cercaBorde = Math.Abs(Close[0] - perfil.Vah) <= ToleranciaNivelTicks * TickSize
                               || Math.Abs(Close[0] - perfil.Val) <= ToleranciaNivelTicks * TickSize;
                if (dentroVA && !cercaBorde) return false;
            }
            return true;
        }

        // ===============================================================
        //  ETAPA 7: CONFLUENCIA Y SCORE
        // ===============================================================
        private void EvaluarSenal()
        {
            if (!fp.Valida) return;
            if (CurrentBar - ultimaSenalBarra < 5) return;   // sin senales encadenadas

            for (int k = 0; k < 2; k++)
            {
                IofDir dir = k == 0 ? IofDir.Largo : IofDir.Corto;
                int signo = (int)dir;

                // --- 1) SWEEP (20) : debe ser reciente y del lado contrario ---
                bool sweepOK = sweepReciente != null
                               && (CurrentBar - sweepBarra) <= VelasValidezSweep
                               && sweepReciente.EsCompra == (dir == IofDir.Corto);
                if (!sweepOK) continue;

                // --- 2) VOLUME PROFILE (20) : nivel institucional o borde del VA ---
                double tol = ToleranciaNivelTicks * TickSize;
                NivelInst nivel = niveles.MasCercano(Close[0], tol);
                bool bordeVa = (perfil.Vah > 0 && Math.Abs(Close[0] - perfil.Vah) <= tol)
                            || (perfil.Val > 0 && Math.Abs(Close[0] - perfil.Val) <= tol);
                bool pocVirgen = perfil.PocAnteriorVirgen
                                 && Math.Abs(Close[0] - perfil.PocAnterior) <= tol;
                bool lvn = perfil.EsLvn(Close[0], UmbralLvn);
                bool perfilOK = nivel != null || bordeVa || pocVirgen || lvn;
                if (!perfilOK) continue;

                // --- 3) ABSORCION (20) ---
                bool absorcionOK = HayAbsorcion(dir);

                // --- 4) STACKED IMBALANCE (15) ---
                int imb = dir == IofDir.Largo ? fp.ImbCompra : fp.ImbVenta;
                bool imbalanceOK = imb >= MinImbalances;

                // --- 5) DELTA (15) : no entrar si el delta contradice ---
                double dFav = signo * fp.DeltaPct;
                bool deltaOK = dFav >= DeltaMinimoPct;
                if (UsarDivergenciaDelta && !deltaOK)
                {
                    // divergencia valida: el delta fue lejos EN CONTRA y el precio no cedio
                    double adverso = dir == IofDir.Largo ? -fp.MinSeen : fp.MaxSeen;
                    deltaOK = fp.VolumenTotal > 0 && adverso / fp.VolumenTotal >= 0.40
                              && (dir == IofDir.Largo ? fp.AtrapadosVendedores : fp.AtrapadosCompradores);
                }
                if (!deltaOK) continue;
                // El CVD de la sesion no puede contradecir la entrada.
                if (!CvdApoya(dir)) continue;

                // --- 6) SUBASTA TERMINADA (10) ---
                bool subastaOK = dir == IofDir.Largo ? fp.SubastaTerminadaAbajo : fp.SubastaTerminadaArriba;

                // --- 7) RECHAZO DEL NIVEL ---
                double rango = Math.Max(High[0] - Low[0], TickSize);
                double mecha = dir == IofDir.Largo ? (Math.Min(Open[0], Close[0]) - Low[0]) / rango
                                                   : (High[0] - Math.Max(Open[0], Close[0])) / rango;
                if (mecha < MechaMinimaSweep) continue;

                // --- 8) CONTEXTO DIRECCIONAL (15m) ---
                bool contextoOK = dir == IofDir.Largo
                                  ? Closes[Idx15][0] > Opens[Idx15][0] || Close[0] > perfil.Poc
                                  : Closes[Idx15][0] < Opens[Idx15][0] || Close[0] < perfil.Poc;
                if (!contextoOK) continue;

                // --- SCORE ---
                int score = 0;
                if (sweepOK)     score += PtsSweep;
                if (perfilOK)    score += PtsPerfil;
                if (absorcionOK) score += PtsAbsorcion;
                if (imbalanceOK) score += PtsImbalance;
                if (deltaOK)     score += PtsDelta;
                if (subastaOK)   score += PtsSubasta;

                if (score < UmbralScore) continue;

                Disparar(dir, score, nivel, imb);
                return;
            }
        }

        private void Disparar(IofDir dir, int score, NivelInst nivel, int imb)
        {
            ultimaSenalBarra = CurrentBar;
            sweepReciente = null;

            double entrada = Close[0];
            double stop = dir == IofDir.Largo
                        ? Low[0]  - StopTicksExtra * TickSize
                        : High[0] + StopTicksExtra * TickSize;
            double r = Math.Abs(entrada - stop);
            double objetivo = dir == IofDir.Largo ? entrada + ObjetivoR * r : entrada - ObjetivoR * r;

            string nom = nivel != null ? nivel.Nombre : "VA/LVN";
            string txt = (dir == IofDir.Largo ? "LARGO" : "CORTO")
                       + " " + Instrument.MasterInstrument.Name
                       + " | score " + score + "/100"
                       + " | " + nom
                       + " | imb " + imb
                       + " | E " + entrada.ToString("F2")
                       + " SL " + stop.ToString("F2")
                       + " TP " + objetivo.ToString("F2");

            if (MostrarSenales)
            {
                Brush c = dir == IofDir.Largo ? ColorLargo : ColorCorto;
                if (dir == IofDir.Largo)
                    Draw.ArrowUp(this, "sig" + CurrentBar, false, 0, Low[0] - 6 * TickSize, c);
                else
                    Draw.ArrowDown(this, "sig" + CurrentBar, false, 0, High[0] + 6 * TickSize, c);
                Draw.Text(this, "txt" + CurrentBar, score.ToString(), 0,
                          dir == IofDir.Largo ? Low[0] - 12 * TickSize : High[0] + 12 * TickSize, c);
                Draw.Line(this, "sl" + CurrentBar, false, 0, stop, -6, stop, Brushes.IndianRed, DashStyleHelper.Dash, 1);
                Draw.Line(this, "tp" + CurrentBar, false, 0, objetivo, -6, objetivo, Brushes.SeaGreen, DashStyleHelper.Dash, 1);
            }

            // Alert() solo tiene sentido en vivo; en historico NT lo ignora y
            // ensucia el log, asi que ni lo llamamos.
            if (State == State.Realtime)
                Alert("iof" + CurrentBar, Priority.High, txt,
                      AlertaSonora ? ArchivoSonido : "", 10, Brushes.Black,
                      dir == IofDir.Largo ? ColorLargo : ColorCorto);
            Print(Time[0] + "  " + txt);
        }

        // ===============================================================
        //  PARAMETROS
        // ===============================================================
        #region 1) Multi-timeframe
        [NinjaScriptProperty] [Range(1, 240)]
        [Display(Name = "Minutos del contexto", Order = 1, GroupName = "1) Multi-timeframe")]
        public int MinutosContexto { get; set; }

        [NinjaScriptProperty] [Range(1, 60)]
        [Display(Name = "Minutos del refinado", Order = 2, GroupName = "1) Multi-timeframe")]
        public int MinutosRefinado { get; set; }
        #endregion

        #region 2) Volume Profile
        [NinjaScriptProperty] [Range(0.5, 0.95)]
        [Display(Name = "Porcentaje del area de valor", Order = 1, GroupName = "2) Volume Profile")]
        public double PorcentajeAreaValor { get; set; }

        [NinjaScriptProperty] [Range(0.1, 1.0)]
        [Display(Name = "Umbral HVN (x POC)", Order = 2, GroupName = "2) Volume Profile")]
        public double UmbralHvn { get; set; }

        [NinjaScriptProperty] [Range(0.01, 0.9)]
        [Display(Name = "Umbral LVN (x POC)", Order = 3, GroupName = "2) Volume Profile")]
        public double UmbralLvn { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Tolerancia a niveles (ticks)", Order = 4, GroupName = "2) Volume Profile")]
        public int ToleranciaNivelTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "No operar dentro del POC", Order = 5, GroupName = "2) Volume Profile")]
        public bool EvitarPoc { get; set; }

        [NinjaScriptProperty] [Range(1, 50)]
        [Display(Name = "Ancho del POC (ticks)", Order = 6, GroupName = "2) Volume Profile")]
        public int AnchoPocTicks { get; set; }
        #endregion

        #region 3) Liquidez
        [NinjaScriptProperty] [Range(2, 20)]
        [Display(Name = "Pivote de swing", Order = 1, GroupName = "3) Liquidez")]
        public int PivoteSwing { get; set; }

        [NinjaScriptProperty] [Range(1, 40)]
        [Display(Name = "Tolerancia Equal High/Low (ticks)", Order = 2, GroupName = "3) Liquidez")]
        public int ToleranciaIgualTicks { get; set; }

        [NinjaScriptProperty] [Range(0.1, 0.9)]
        [Display(Name = "Mecha minima del sweep", Order = 3, GroupName = "3) Liquidez",
                 Description = "Fraccion del rango de la vela. Distingue un sweep real de una ruptura.")]
        public double MechaMinimaSweep { get; set; }

        [NinjaScriptProperty] [Range(0.5, 10.0)]
        [Display(Name = "Volumen minimo del sweep (x media)", Order = 4, GroupName = "3) Liquidez")]
        public double VolumenMinimoSweep { get; set; }

        [NinjaScriptProperty] [Range(1, 50)]
        [Display(Name = "Validez del sweep (velas)", Order = 5, GroupName = "3) Liquidez")]
        public int VelasValidezSweep { get; set; }
        #endregion

        #region 4) Imbalances
        [NinjaScriptProperty] [Range(1.5, 10.0)]
        [Display(Name = "Ratio de imbalance (3, 4, 5...)", Order = 1, GroupName = "4) Imbalances")]
        public double RatioImbalance { get; set; }

        [NinjaScriptProperty] [Range(1, 20)]
        [Display(Name = "Imbalances consecutivas minimas", Order = 2, GroupName = "4) Imbalances")]
        public int MinImbalances { get; set; }
        #endregion

        #region 5) Delta
        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Delta minimo (porcentaje)", Order = 1, GroupName = "5) Delta")]
        public double DeltaMinimoPct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Aceptar divergencia de delta", Order = 2, GroupName = "5) Delta",
                 Description = "Permite la senal si el delta fue lejos EN CONTRA y el precio no cedio (absorcion).")]
        public bool UsarDivergenciaDelta { get; set; }
        #endregion

        #region 6) Absorcion
        [NinjaScriptProperty] [Range(1, 20)]
        [Display(Name = "Velas sin avance", Order = 1, GroupName = "6) Absorcion")]
        public int AbsorcionVelas { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Rango maximo sin avance (ticks)", Order = 2, GroupName = "6) Absorcion")]
        public int AbsorcionRangoTicks { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Delta acumulado minimo", Order = 3, GroupName = "6) Absorcion")]
        public double AbsorcionDeltaMin { get; set; }
        #endregion

        #region 7) Score
        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Score minimo para senal", Order = 1, GroupName = "7) Score",
                 Description = "85 exige practicamente todas las condiciones y da muy pocas senales. "
                             + "Bajalo a 70-75 para medir el compromiso entre calidad y frecuencia.")]
        public int UmbralScore { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: sweep", Order = 2, GroupName = "7) Score")]
        public int PtsSweep { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: volume profile", Order = 3, GroupName = "7) Score")]
        public int PtsPerfil { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: absorcion", Order = 4, GroupName = "7) Score")]
        public int PtsAbsorcion { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: stacked imbalance", Order = 5, GroupName = "7) Score")]
        public int PtsImbalance { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: delta", Order = 6, GroupName = "7) Score")]
        public int PtsDelta { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: subasta terminada", Order = 7, GroupName = "7) Score")]
        public int PtsSubasta { get; set; }
        #endregion

        #region 8) Filtros
        [NinjaScriptProperty]
        [Display(Name = "Filtrar por sesion", Order = 1, GroupName = "8) Filtros")]
        public bool UsarSesion { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Sesion inicio (HHMM ET)", Order = 2, GroupName = "8) Filtros")]
        public int SesionIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Sesion fin (HHMM ET)", Order = 3, GroupName = "8) Filtros")]
        public int SesionFin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Evitar lunch", Order = 4, GroupName = "8) Filtros")]
        public bool EvitarLunch { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Lunch inicio (HHMM ET)", Order = 5, GroupName = "8) Filtros")]
        public int LunchIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Lunch fin (HHMM ET)", Order = 6, GroupName = "8) Filtros")]
        public int LunchFin { get; set; }

        [NinjaScriptProperty] [Range(-12, 12)]
        [Display(Name = "Ajuste horario a ET", Order = 7, GroupName = "8) Filtros")]
        public int OffsetHorasET { get; set; }

        [NinjaScriptProperty] [Range(0.0, 5.0)]
        [Display(Name = "Volumen minimo (x media)", Order = 8, GroupName = "8) Filtros")]
        public double VolumenMinimoMedia { get; set; }

        [NinjaScriptProperty] [Range(0, 200)]
        [Display(Name = "Rango minimo de la vela (ticks)", Order = 9, GroupName = "8) Filtros")]
        public int AtrMinimoTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Evitar mercado balanceado", Order = 10, GroupName = "8) Filtros",
                 Description = "No opera si el precio lleva rato dentro del area de valor lejos de los bordes.")]
        public bool EvitarBalanceado { get; set; }
        #endregion

        #region 9) Riesgo mostrado
        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Ticks extra del stop", Order = 1, GroupName = "9) Riesgo mostrado")]
        public int StopTicksExtra { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Objetivo (multiplo de R)", Order = 2, GroupName = "9) Riesgo mostrado")]
        public double ObjetivoR { get; set; }
        #endregion

        #region 10) Visual
        [NinjaScriptProperty]
        [Display(Name = "Mostrar perfil (POC/VAH/VAL)", Order = 1, GroupName = "10) Visual")]
        public bool MostrarPerfil { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar liquidez y sweeps", Order = 2, GroupName = "10) Visual")]
        public bool MostrarLiquidez { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar imbalances", Order = 3, GroupName = "10) Visual")]
        public bool MostrarImbalances { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar absorcion", Order = 4, GroupName = "10) Visual")]
        public bool MostrarAbsorcion { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar senales", Order = 5, GroupName = "10) Visual")]
        public bool MostrarSenales { get; set; }

        [XmlIgnore]
        [Display(Name = "Color largo", Order = 6, GroupName = "10) Visual")]
        public Brush ColorLargo { get; set; }
        [Browsable(false)]
        public string ColorLargoSerialize
        {
            get { return Serialize.BrushToString(ColorLargo); }
            set { ColorLargo = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color corto", Order = 7, GroupName = "10) Visual")]
        public Brush ColorCorto { get; set; }
        [Browsable(false)]
        public string ColorCortoSerialize
        {
            get { return Serialize.BrushToString(ColorCorto); }
            set { ColorCorto = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color del perfil", Order = 8, GroupName = "10) Visual")]
        public Brush ColorPerfil { get; set; }
        [Browsable(false)]
        public string ColorPerfilSerialize
        {
            get { return Serialize.BrushToString(ColorPerfil); }
            set { ColorPerfil = Serialize.StringToBrush(value); }
        }
        #endregion

        #region 11) Alertas
        [NinjaScriptProperty]
        [Display(Name = "Alerta sonora", Order = 1, GroupName = "11) Alertas")]
        public bool AlertaSonora { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Archivo de sonido", Order = 2, GroupName = "11) Alertas")]
        public string ArchivoSonido { get; set; }
        #endregion
    }
}
