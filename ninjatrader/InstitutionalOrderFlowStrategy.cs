// ===================================================================
//  INSTITUTIONAL ORDER FLOW STRATEGY - NinjaTrader 8 (NinjaScript / C#)
//  Version EJECUTABLE del indicador InstitutionalOrderFlow.
//  Misma logica de lectura; aqui ademas entra, gestiona y sale.
//
//  Aparece en Strategy Analyzer -> Backtest (el indicador no, los
//  indicadores no se backtestean).
//
//  ARQUITECTURA MULTI-TIMEFRAME
//    15m  CONTEXTO   perfil de volumen, niveles institucionales, sesgo
//     5m  REFINADO   estructura de liquidez, swings, EQH/EQL, pools
//     1m  EJECUCION  footprint, imbalances, absorcion, delta, entrada
//
//  SCORE (100 puntos)
//    Sweep de liquidez 20 | Zona del perfil 20 | Absorcion 20
//    Stacked imbalance 15 | Delta 15          | Subasta terminada 10
//
//  Sweep, zona de perfil, delta, mecha de rechazo y contexto de 15m son
//  PUERTAS DURAS: sin ellas no se evalua nada. El suelo que dejan es 55.
//  Por eso con umbral 85 hacen falta dos de los tres opcionales (y en la
//  practica la absorcion pasa a ser casi obligatoria), mientras que con
//  70 basta con uno. El umbral por defecto aqui es 70 para que el
//  backtest tenga una muestra medible; subelo despues si quieres.
//
//  GESTION
//  Objetivo lejano (3R) para dejar correr al ganador, parcial opcional
//  en 1.5R y breakeven a partir de 2R. El BE arranca tarde a proposito:
//  con objetivo lejano, mover a BE en 1R mata la esperanza matematica
//  (medido: PF 1.27 -> 0.80, con el WR cayendo del 35% al 12%).
//
//  LIMITES DIARIOS
//  PerdidaMaximaDia corta el dia entero al tocarse. No es lo mismo perder
//  800 en un dia que 1500.
//
//  DIAGNOSTICO
//  Al terminar imprime en NinjaScript Output el embudo completo: cuantas
//  senales mueren en cada etapa y cuantas operaciones tendrias con cada
//  umbral de score. Eso te ahorra tener que relanzar el backtest para
//  cada umbral.
//
//  REQUISITO: barras Volumetric (licencia Lifetime u Order Flow+).
//  APLICAR SOBRE: grafico / backtest de 1 minuto de ES.
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

namespace NinjaTrader.NinjaScript.Strategies
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

    public class InstitutionalOrderFlowStrategy : Strategy
    {
        // ---------- indices de series ----------
        private int IdxTick = 1;  // 1 tick: SOLO para que las ordenes se llenen intrabar
        private int Idx5    = 2;  // refinado
        private int Idx15   = 3;  // contexto
        private int IdxVol  = 4;  // footprint (Volumetric, misma TF que la primaria)

        // ---------- modulos ----------
        private PerfilVolumenSesion    perfil;
        private NivelesInstitucionales niveles;
        private DetectorLiquidez       liq5;

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

        // ---------- gestion de la operacion ----------
        private double entradaPrecio = 0, stopPrecio = 0, riesgoR = 0;
        private bool   beAplicado = false;
        private int    parcialHecho = 0;
        private string senalActual = "";

        // ---------- limites diarios ----------
        private double pnlInicioDia = 0;
        private bool   diaBloqueado = false;

        // ---------- diagnostico (embudo) ----------
        private int nBarras, nFiltro, nSweep, nPerfil, nDelta, nCvd, nMecha, nContexto, nScore, nSenal;
        private readonly List<int> scoresVistos = new List<int>();

        // ===============================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Version ejecutable del detector InstitutionalOrderFlow: "
                            + "Volume Profile, liquidez, footprint, imbalances, absorcion y delta.";
                Name        = "InstitutionalOrderFlowStrategy";
                Calculate   = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 60;
                BarsRequiredToTrade = 30;
                IncludeCommission = true;
                // Necesario para que cada iteracion del optimizador arranque
                // limpia: sin esto el estado se arrastra entre pruebas.
                IsInstantiatedOnEachOptimizationIteration = true;

                // --- 1) Multi-timeframe ---
                MinutosContexto = 15;
                MinutosRefinado = 5;
                UsarSerieTick   = true;

                // --- 2) Volume Profile ---
                PorcentajeAreaValor  = 0.70;
                UmbralLvn            = 0.25;
                ToleranciaNivelTicks = 8;
                EvitarPoc            = true;
                AnchoPocTicks        = 4;

                // --- 3) Liquidez ---
                PivoteSwing          = 4;
                ToleranciaIgualTicks = 4;
                MechaMinimaSweep     = 0.40;
                VolumenMinimoSweep   = 1.2;
                VelasValidezSweep    = 6;

                // --- 4) Imbalances ---
                RatioImbalance = 3.0;
                MinImbalances  = 3;

                // --- 5) Delta ---
                DeltaMinimoPct       = 0.15;
                UsarDivergenciaDelta = true;
                UsarCvd              = true;

                // --- 6) Absorcion ---
                AbsorcionVelas      = 3;
                AbsorcionRangoTicks = 6;
                AbsorcionDeltaMin   = 0.25;

                // --- 7) Score ---
                UmbralScore  = 70;
                PtsSweep     = 20; PtsPerfil = 20; PtsAbsorcion = 20;
                PtsImbalance = 15; PtsDelta  = 15; PtsSubasta   = 10;

                // --- 8) Filtros ---
                UsarSesion         = true;
                SesionIni          = 930;
                SesionFin          = 1530;
                EvitarLunch        = true;
                LunchIni           = 1200;
                LunchFin           = 1330;
                OffsetHorasET      = 0;
                VolumenMinimoMedia = 0.8;
                AtrMinimoTicks     = 8;
                EvitarBalanceado   = true;

                // --- 9) Riesgo y gestion ---
                Contratos      = 1;
                StopTicksExtra = 4;
                ObjetivoR      = 3.0;
                UsarParcial    = true;
                ParcialEnR     = 1.5;
                UsarBreakeven  = true;
                BeDesdeR       = 2.0;
                UsarTrailing   = false;
                TrailingTicks  = 24;

                // --- 10) Limites diarios ---
                PerdidaMaximaDia = 800;
                ObjetivoDia      = 0;

                // --- 11) Diagnostico ---
                ImprimirEmbudo = true;
            }
            else if (State == State.Configure)
            {
                // ATENCION: al usar AddDataSeries el OrderFillResolution deja de
                // aplicarse. Por eso las ordenes se mandan a la serie de 1 tick.
                if (UsarSerieTick) { IdxTick = 1; AddDataSeries(BarsPeriodType.Tick, 1); }
                else IdxTick = 0;

                Idx5  = UsarSerieTick ? 2 : 1; AddDataSeries(BarsPeriodType.Minute, MinutosRefinado);
                Idx15 = Idx5 + 1;              AddDataSeries(BarsPeriodType.Minute, MinutosContexto);
                IdxVol = Idx15 + 1;
                AddVolumetric(null, BarsPeriod.BarsPeriodType, BarsPeriod.Value,
                              VolumetricDeltaType.BidAsk, 1);
            }
            else if (State == State.DataLoaded)
            {
                ResetEstado();
            }
            else if (State == State.Terminated)
            {
                if (ImprimirEmbudo) VolcarEmbudo();
            }
        }

        private void ResetEstado()
        {
            perfil  = new PerfilVolumenSesion(TickSize);
            niveles = new NivelesInstitucionales();
            liq5    = new DetectorLiquidez();
            cvdHist.Clear(); cvdSesion = 0; cvdBarraInicioSesion = 0;
            semanaActual = -1; sweepReciente = null; sweepBarra = -999;
            volMedia = 0; ultimaSenalBarra = -999;
            absAcumDelta = 0; absBarras = 0; absPrecioRef = double.NaN;
            entradaPrecio = stopPrecio = riesgoR = 0;
            beAplicado = false; parcialHecho = 0; senalActual = "";
            pnlInicioDia = 0; diaBloqueado = false;
            nBarras = nFiltro = nSweep = nPerfil = nDelta = nCvd = 0;
            nMecha = nContexto = nScore = nSenal = 0;
            scoresVistos.Clear();
        }

        // ===============================================================
        //  BUCLE PRINCIPAL
        // ===============================================================
        protected override void OnBarUpdate()
        {
            if (BarsInProgress == IdxVol) { LeerFootprint(); return; }
            if (BarsInProgress == Idx5)   { ActualizarLiquidez5(); return; }
            if (BarsInProgress == Idx15)  { return; }
            if (BarsInProgress != 0)      { return; }   // serie de tick: solo para fills

            if (CurrentBar < 30 || CurrentBars[Idx5] < 10 || CurrentBars[Idx15] < 5) return;
            if (CurrentBars[IdxVol] < 1) return;
            if (UsarSerieTick && CurrentBars[IdxTick] < 1) return;

            DateTime et = Time[0].AddHours(OffsetHorasET);
            int hmm = et.Hour * 100 + et.Minute;

            // ---------- corte de sesion ----------
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

                // Reinicio del contador de perdida diaria.
                pnlInicioDia = PnlAcumulado();
                diaBloqueado = false;
            }

            // ---------- contexto ----------
            AlimentarPerfil();
            niveles.Alimentar(High[0], Low[0], hmm, 30, 60);
            perfil.RecalcularAreaValor(PorcentajeAreaValor);

            if (perfil.PocAnteriorVirgen && High[0] >= perfil.PocAnterior && Low[0] <= perfil.PocAnterior)
                perfil.PocAnteriorVirgen = false;

            if (fp.Valida)
            {
                cvdSesion += fp.Delta;
                cvdHist.Add(cvdSesion);
                if (cvdHist.Count > 500) cvdHist.RemoveAt(0);
            }

            volMedia = volMedia <= 0 ? Volume[0] : volMedia * 0.95 + Volume[0] * 0.05;
            double volRatio = volMedia > 0 ? Volume[0] / volMedia : 1.0;

            ActualizarAbsorcion();
            DetectarSweep(volRatio);

            // ---------- gestion de la posicion abierta ----------
            if (Position.MarketPosition != MarketPosition.Flat) { GestionarPosicion(); return; }
            if (senalActual.Length > 0)   // acabamos de quedar planos: limpiar
            { senalActual = ""; riesgoR = 0; beAplicado = false; parcialHecho = 0; }

            // ---------- limites diarios ----------
            if (RevisarLimitesDia()) return;

            nBarras++;
            if (!PasaFiltros(hmm, volRatio)) return;
            nFiltro++;

            EvaluarSenal();
        }

        // ===============================================================
        //  LIMITES DIARIOS
        //  Perder 800 en un dia no es lo mismo que perder 1500: en cuanto
        //  se toca el limite, la estrategia deja de operar hasta manana.
        // ===============================================================
        private double PnlAcumulado()
        {
            return SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
        }

        private bool RevisarLimitesDia()
        {
            if (diaBloqueado) return true;
            double pnlDia = PnlAcumulado() - pnlInicioDia;
            if (PerdidaMaximaDia > 0 && pnlDia <= -Math.Abs(PerdidaMaximaDia)) { diaBloqueado = true; return true; }
            if (ObjetivoDia > 0 && pnlDia >= ObjetivoDia) { diaBloqueado = true; return true; }
            return false;
        }

        // ===============================================================
        //  FOOTPRINT (una pasada por vela, resultado cacheado)
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
                double cl = Closes[IdxVol][0];
                if (hi <= lo) return;
                int pasos = (int)Math.Round((hi - lo) / TickSize) + 1;
                if (pasos < 2 || pasos > 2000) return;

                fp.Delta        = v.BarDelta;
                fp.VolumenTotal = Volumes[IdxVol][0];
                fp.DeltaPct     = fp.VolumenTotal > 0 ? fp.Delta / fp.VolumenTotal : 0;
                fp.MaxSeen      = v.MaxSeenDelta;
                fp.MinSeen      = v.MinSeenDelta;

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

                    // Imbalance en DIAGONAL: el comprador agresivo paga el ask y
                    // el vendedor pega en el bid, no compiten en el mismo nivel.
                    if (i > 0)
                    {
                        if (ask > 0 && bidAnterior > 0 && ask >= bidAnterior * RatioImbalance)
                        { rachaC++; if (rachaC > fp.ImbCompra) { fp.ImbCompra = rachaC; fp.ImbPrecioAlto = p; } }
                        else rachaC = 0;
                        if (bidAnterior > 0 && ask > 0 && bidAnterior >= ask * RatioImbalance)
                        { rachaV++; if (rachaV > fp.ImbVenta) { fp.ImbVenta = rachaV; fp.ImbPrecioBajo = p; } }
                        else rachaV = 0;
                    }
                    if (i == 0)         { fp.VolExtremoBajo = tot; fp.SubastaTerminadaAbajo = ask <= 0; }
                    if (i == pasos - 1) { fp.VolExtremoAlto = tot; fp.SubastaTerminadaArriba = bid <= 0; }
                    bidAnterior = bid;
                }
                if (maxVol <= 0) return;

                fp.Poc    = poc;
                fp.PocRel = (poc - lo) / (hi - lo);

                double rango = hi - lo;
                fp.AtrapadosCompradores = fp.DeltaPct >  0.15 && (cl - lo) / rango < 0.35;
                fp.AtrapadosVendedores  = fp.DeltaPct < -0.15 && (hi - cl) / rango < 0.35;

                fp.Valida = true;
            }
            catch (Exception ex)
            {
                Print("Volumetric no disponible (necesitas Lifetime u Order Flow+): " + ex.Message);
            }
        }

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
        //  LIQUIDEZ (swings confirmados en 5m, no repintan)
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
            if (s != null) { sweepReciente = s; sweepBarra = CurrentBar; }
        }

        // ===============================================================
        //  ABSORCION
        // ===============================================================
        private void ActualizarAbsorcion()
        {
            if (!fp.Valida) { absBarras = 0; absAcumDelta = 0; return; }
            double rangoMax = AbsorcionRangoTicks * TickSize;
            if (double.IsNaN(absPrecioRef)) absPrecioRef = Close[0];

            if (Math.Abs(Close[0] - absPrecioRef) <= rangoMax)
            {
                absBarras++;
                absAcumDelta += fp.Delta;
            }
            else { absBarras = 0; absAcumDelta = 0; absPrecioRef = Close[0]; }
        }

        private bool HayAbsorcion(IofDir dir)
        {
            if (absBarras < AbsorcionVelas || fp.VolumenTotal <= 0) return false;
            double ratio = absAcumDelta / Math.Max(fp.VolumenTotal * absBarras, 1);
            // Para un LARGO queremos delta VENDEDOR sin que el precio ceda:
            // el vendedor esta siendo absorbido.
            return dir == IofDir.Largo ? ratio <= -AbsorcionDeltaMin
                                       : ratio >=  AbsorcionDeltaMin;
        }

        /// <summary>
        /// CVD de la sesion. True si el delta acumulado no contradice, o si
        /// hay divergencia valida (nuevo extremo de precio sin acompanamiento
        /// del CVD: movimiento sin agresion detras).
        /// </summary>
        private bool CvdApoya(IofDir dir)
        {
            if (!UsarCvd) return true;
            int n = cvdHist.Count;
            if (n < 20) return true;

            // ventana SIEMPRE dentro de la misma sesion: el CVD se reinicia
            // cada dia, compararlo entre sesiones no significa nada.
            int ventana = Math.Min(n - 1, Math.Min(30, CurrentBar - cvdBarraInicioSesion));
            if (ventana < 10) return true;

            double dCvd = cvdSesion - cvdHist[n - 1 - ventana];

            if (dir == IofDir.Largo)
            {
                if (Low[0] <= MIN(Low, ventana)[1] && dCvd > 0) return true;   // divergencia alcista
                return dCvd > 0 || cvdSesion > 0;
            }
            if (High[0] >= MAX(High, ventana)[1] && dCvd < 0) return true;     // divergencia bajista
            return dCvd < 0 || cvdSesion < 0;
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

            // Dentro del POC el mercado esta en equilibrio: no hay
            // desplazamiento que capturar.
            if (EvitarPoc && perfil.Poc > 0
                && Math.Abs(Close[0] - perfil.Poc) <= AnchoPocTicks * TickSize) return false;

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
        //  CONFLUENCIA Y SCORE
        // ===============================================================
        private void EvaluarSenal()
        {
            if (!fp.Valida) return;
            if (CurrentBar - ultimaSenalBarra < 5) return;

            for (int k = 0; k < 2; k++)
            {
                IofDir dir = k == 0 ? IofDir.Largo : IofDir.Corto;
                int signo = (int)dir;

                // --- 1) SWEEP (20) ---
                bool sweepOK = sweepReciente != null
                               && (CurrentBar - sweepBarra) <= VelasValidezSweep
                               && sweepReciente.EsCompra == (dir == IofDir.Corto);
                if (!sweepOK) continue;
                nSweep++;

                // --- 2) VOLUME PROFILE (20) ---
                double tol = ToleranciaNivelTicks * TickSize;
                NivelInst nivel = niveles.MasCercano(Close[0], tol);
                bool bordeVa = (perfil.Vah > 0 && Math.Abs(Close[0] - perfil.Vah) <= tol)
                            || (perfil.Val > 0 && Math.Abs(Close[0] - perfil.Val) <= tol);
                bool pocVirgen = perfil.PocAnteriorVirgen
                                 && Math.Abs(Close[0] - perfil.PocAnterior) <= tol;
                bool lvn = perfil.EsLvn(Close[0], UmbralLvn);
                bool perfilOK = nivel != null || bordeVa || pocVirgen || lvn;
                if (!perfilOK) continue;
                nPerfil++;

                // --- 3) ABSORCION (20) ---
                bool absorcionOK = HayAbsorcion(dir);

                // --- 4) STACKED IMBALANCE (15) ---
                int imb = dir == IofDir.Largo ? fp.ImbCompra : fp.ImbVenta;
                bool imbalanceOK = imb >= MinImbalances;

                // --- 5) DELTA (15) ---
                bool deltaOK = signo * fp.DeltaPct >= DeltaMinimoPct;
                if (UsarDivergenciaDelta && !deltaOK)
                {
                    double adverso = dir == IofDir.Largo ? -fp.MinSeen : fp.MaxSeen;
                    deltaOK = fp.VolumenTotal > 0 && adverso / fp.VolumenTotal >= 0.40
                              && (dir == IofDir.Largo ? fp.AtrapadosVendedores : fp.AtrapadosCompradores);
                }
                if (!deltaOK) continue;
                nDelta++;

                if (!CvdApoya(dir)) continue;
                nCvd++;

                // --- 6) SUBASTA TERMINADA (10) ---
                bool subastaOK = dir == IofDir.Largo ? fp.SubastaTerminadaAbajo : fp.SubastaTerminadaArriba;

                // --- 7) RECHAZO DEL NIVEL ---
                double rango = Math.Max(High[0] - Low[0], TickSize);
                double mecha = dir == IofDir.Largo ? (Math.Min(Open[0], Close[0]) - Low[0]) / rango
                                                   : (High[0] - Math.Max(Open[0], Close[0])) / rango;
                if (mecha < MechaMinimaSweep) continue;
                nMecha++;

                // --- 8) CONTEXTO DIRECCIONAL (15m) ---
                bool contextoOK = dir == IofDir.Largo
                                  ? Closes[Idx15][0] > Opens[Idx15][0] || Close[0] > perfil.Poc
                                  : Closes[Idx15][0] < Opens[Idx15][0] || Close[0] < perfil.Poc;
                if (!contextoOK) continue;
                nContexto++;

                // --- SCORE ---
                int score = 0;
                if (sweepOK)     score += PtsSweep;
                if (perfilOK)    score += PtsPerfil;
                if (absorcionOK) score += PtsAbsorcion;
                if (imbalanceOK) score += PtsImbalance;
                if (deltaOK)     score += PtsDelta;
                if (subastaOK)   score += PtsSubasta;
                scoresVistos.Add(score);
                nScore++;

                if (score < UmbralScore) continue;

                Disparar(dir, score, nivel);
                return;
            }
        }

        // ===============================================================
        //  EJECUCION
        // ===============================================================
        private void Disparar(IofDir dir, int score, NivelInst nivel)
        {
            ultimaSenalBarra = CurrentBar;
            sweepReciente = null;
            nSenal++;

            entradaPrecio = Close[0];
            stopPrecio = dir == IofDir.Largo
                       ? Low[0]  - StopTicksExtra * TickSize
                       : High[0] + StopTicksExtra * TickSize;
            riesgoR = Math.Abs(entradaPrecio - stopPrecio);
            if (riesgoR < TickSize * 2) return;   // stop absurdamente pegado

            beAplicado = false;
            parcialHecho = 0;

            string sig = (dir == IofDir.Largo ? "IOF_L" : "IOF_S") + score;
            senalActual = sig;
            int idx = UsarSerieTick ? IdxTick : 0;

            SetStopLoss(sig, CalculationMode.Price, stopPrecio, false);
            SetProfitTarget(sig, CalculationMode.Price,
                            dir == IofDir.Largo ? entradaPrecio + ObjetivoR * riesgoR
                                                : entradaPrecio - ObjetivoR * riesgoR);

            if (dir == IofDir.Largo) EnterLong(idx, Contratos, sig);
            else                     EnterShort(idx, Contratos, sig);

            if (ImprimirEmbudo)
                Print(Time[0] + "  " + (dir == IofDir.Largo ? "LARGO " : "CORTO ")
                      + "score " + score + "/100  " + (nivel != null ? nivel.Nombre : "VA/LVN")
                      + "  E " + entradaPrecio.ToString("F2")
                      + "  SL " + stopPrecio.ToString("F2")
                      + "  R " + riesgoR.ToString("F2") + " pts");
        }

        /// <summary>
        /// Parcial, breakeven y trailing. El objetivo es dejar correr al
        /// ganador (por eso el TP esta en 3R) sin devolver toda la operacion,
        /// pero SIN mover el stop a BE demasiado pronto: mover a BE en 1R con
        /// objetivo lejano destruye la esperanza matematica.
        /// </summary>
        private void GestionarPosicion()
        {
            if (riesgoR <= 0 || senalActual.Length == 0) return;
            bool largo = Position.MarketPosition == MarketPosition.Long;
            // Precio real de llenado, no el cierre de la vela de senal.
            double entrada = Position.AveragePrice;
            double avanceR = (largo ? Close[0] - entrada : entrada - Close[0]) / riesgoR;
            string sig = senalActual;
            int idx = UsarSerieTick ? IdxTick : 0;

            // --- parcial ---
            if (UsarParcial && parcialHecho == 0 && Position.Quantity >= 2 && avanceR >= ParcialEnR)
            {
                int mitad = Position.Quantity / 2;
                if (mitad > 0)
                {
                    if (largo) ExitLong(idx, mitad, "PARC", sig);
                    else       ExitShort(idx, mitad, "PARC", sig);
                    parcialHecho = 1;
                }
            }

            // --- breakeven (tarde a proposito) ---
            if (UsarBreakeven && !beAplicado && avanceR >= BeDesdeR)
            {
                SetStopLoss(sig, CalculationMode.Price, entrada, false);
                beAplicado = true;
            }

            // --- trailing por ticks ---
            if (UsarTrailing && avanceR >= BeDesdeR)
            {
                double nuevo = largo ? Close[0] - TrailingTicks * TickSize
                                     : Close[0] + TrailingTicks * TickSize;
                bool mejora = largo ? nuevo > stopPrecio : nuevo < stopPrecio;
                if (mejora) { stopPrecio = nuevo; SetStopLoss(sig, CalculationMode.Price, nuevo, false); }
            }
        }

        // ===============================================================
        //  DIAGNOSTICO: donde muere cada senal
        // ===============================================================
        private void VolcarEmbudo()
        {
            if (nBarras == 0) return;
            Print("");
            Print("=== EMBUDO InstitutionalOrderFlowStrategy ===");
            Print("  umbral score      : " + UmbralScore);
            Print("  barras evaluadas  : " + nBarras);
            Print("  pasan filtros     : " + nFiltro);
            Print("  con sweep valido  : " + nSweep);
            Print("  + zona de perfil  : " + nPerfil);
            Print("  + delta OK        : " + nDelta);
            Print("  + CVD OK          : " + nCvd);
            Print("  + mecha de rechazo: " + nMecha);
            Print("  + contexto 15m    : " + nContexto);
            Print("  llegan al score   : " + nScore);
            Print("  SUPERAN el umbral : " + nSenal);
            if (scoresVistos.Count > 0)
            {
                int[] cortes = { 55, 65, 70, 75, 80, 85, 90, 100 };
                Print("  --- cuantas senales tendrias con cada umbral ---");
                foreach (int c in cortes)
                {
                    int n = 0;
                    foreach (int s in scoresVistos) if (s >= c) n++;
                    Print("    umbral " + c + " -> " + n + " senales");
                }
            }
            Print("=============================================");
        }

        // ===============================================================
        //  PARAMETROS
        // ===============================================================
        #region 1) Multi-timeframe
        [NinjaScriptProperty] [Range(1, 240)]
        [Display(Name = "Minutos del contexto (HTF)", Order = 1, GroupName = "1) Multi-timeframe")]
        public int MinutosContexto { get; set; }

        [NinjaScriptProperty] [Range(1, 60)]
        [Display(Name = "Minutos del refinado", Order = 2, GroupName = "1) Multi-timeframe")]
        public int MinutosRefinado { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Serie de 1 tick (fills exactos)", Order = 3, GroupName = "1) Multi-timeframe",
                 Description = "Con AddDataSeries el Order Fill Resolution deja de aplicar. "
                             + "Activado da fills realistas pero el backtest tarda mucho mas.")]
        public bool UsarSerieTick { get; set; }
        #endregion

        #region 2) Volume Profile
        [NinjaScriptProperty] [Range(0.5, 0.95)]
        [Display(Name = "Porcentaje del area de valor", Order = 1, GroupName = "2) Volume Profile")]
        public double PorcentajeAreaValor { get; set; }

        [NinjaScriptProperty] [Range(0.01, 0.9)]
        [Display(Name = "Umbral LVN (volumen relativo)", Order = 2, GroupName = "2) Volume Profile")]
        public double UmbralLvn { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Tolerancia a un nivel (ticks)", Order = 3, GroupName = "2) Volume Profile")]
        public int ToleranciaNivelTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "No operar dentro del POC", Order = 4, GroupName = "2) Volume Profile")]
        public bool EvitarPoc { get; set; }

        [NinjaScriptProperty] [Range(0, 50)]
        [Display(Name = "Ancho del POC (ticks)", Order = 5, GroupName = "2) Volume Profile")]
        public int AnchoPocTicks { get; set; }
        #endregion

        #region 3) Liquidez
        [NinjaScriptProperty] [Range(1, 20)]
        [Display(Name = "Pivote del swing (barras 5m)", Order = 1, GroupName = "3) Liquidez")]
        public int PivoteSwing { get; set; }

        [NinjaScriptProperty] [Range(0, 40)]
        [Display(Name = "Tolerancia EQH/EQL (ticks)", Order = 2, GroupName = "3) Liquidez")]
        public int ToleranciaIgualTicks { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Mecha minima del sweep", Order = 3, GroupName = "3) Liquidez")]
        public double MechaMinimaSweep { get; set; }

        [NinjaScriptProperty] [Range(0.0, 10.0)]
        [Display(Name = "Volumen minimo del sweep (x media)", Order = 4, GroupName = "3) Liquidez")]
        public double VolumenMinimoSweep { get; set; }

        [NinjaScriptProperty] [Range(1, 60)]
        [Display(Name = "Validez del sweep (velas 1m)", Order = 5, GroupName = "3) Liquidez")]
        public int VelasValidezSweep { get; set; }
        #endregion

        #region 4) Imbalances
        [NinjaScriptProperty] [Range(1.1, 20.0)]
        [Display(Name = "Ratio de imbalance", Order = 1, GroupName = "4) Imbalances")]
        public double RatioImbalance { get; set; }

        [NinjaScriptProperty] [Range(1, 20)]
        [Display(Name = "Imbalances apiladas minimas", Order = 2, GroupName = "4) Imbalances")]
        public int MinImbalances { get; set; }
        #endregion

        #region 5) Delta
        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Delta minimo (porcentaje)", Order = 1, GroupName = "5) Delta")]
        public double DeltaMinimoPct { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Aceptar divergencia de delta", Order = 2, GroupName = "5) Delta")]
        public bool UsarDivergenciaDelta { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Filtrar por CVD de sesion", Order = 3, GroupName = "5) Delta")]
        public bool UsarCvd { get; set; }
        #endregion

        #region 6) Absorcion
        [NinjaScriptProperty] [Range(1, 30)]
        [Display(Name = "Velas de absorcion", Order = 1, GroupName = "6) Absorcion")]
        public int AbsorcionVelas { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Rango de absorcion (ticks)", Order = 2, GroupName = "6) Absorcion")]
        public int AbsorcionRangoTicks { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Delta acumulado minimo", Order = 3, GroupName = "6) Absorcion")]
        public double AbsorcionDeltaMin { get; set; }
        #endregion

        #region 7) Score
        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Umbral de score", Order = 1, GroupName = "7) Score",
                 Description = "Puertas duras (sweep, perfil, delta, mecha, contexto) dejan un "
                             + "suelo de 55. Con 85 hacen falta 2 de los 3 opcionales; con 70, uno.")]
        public int UmbralScore { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: sweep", Order = 2, GroupName = "7) Score")]
        public int PtsSweep { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Puntos: zona del perfil", Order = 3, GroupName = "7) Score")]
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
        [Display(Name = "Inicio de sesion (HHMM ET)", Order = 2, GroupName = "8) Filtros")]
        public int SesionIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Fin de sesion (HHMM ET)", Order = 3, GroupName = "8) Filtros")]
        public int SesionFin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Evitar el lunch", Order = 4, GroupName = "8) Filtros")]
        public bool EvitarLunch { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Inicio del lunch (HHMM ET)", Order = 5, GroupName = "8) Filtros")]
        public int LunchIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Fin del lunch (HHMM ET)", Order = 6, GroupName = "8) Filtros")]
        public int LunchFin { get; set; }

        [NinjaScriptProperty] [Range(-12, 12)]
        [Display(Name = "Offset horario a ET", Order = 7, GroupName = "8) Filtros",
                 Description = "0 si NinjaTrader ya muestra hora de Nueva York.")]
        public int OffsetHorasET { get; set; }

        [NinjaScriptProperty] [Range(0.0, 10.0)]
        [Display(Name = "Volumen minimo (x media)", Order = 8, GroupName = "8) Filtros")]
        public double VolumenMinimoMedia { get; set; }

        [NinjaScriptProperty] [Range(0, 200)]
        [Display(Name = "Rango minimo de vela (ticks)", Order = 9, GroupName = "8) Filtros")]
        public int AtrMinimoTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Evitar mercado balanceado", Order = 10, GroupName = "8) Filtros")]
        public bool EvitarBalanceado { get; set; }
        #endregion

        #region 9) Riesgo y gestion
        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Contratos", Order = 1, GroupName = "9) Riesgo y gestion")]
        public int Contratos { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Ticks extra del stop", Order = 2, GroupName = "9) Riesgo y gestion")]
        public int StopTicksExtra { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Objetivo (multiplo de R)", Order = 3, GroupName = "9) Riesgo y gestion")]
        public double ObjetivoR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cerrar parcial", Order = 4, GroupName = "9) Riesgo y gestion",
                 Description = "Requiere al menos 2 contratos.")]
        public bool UsarParcial { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Parcial en (R)", Order = 5, GroupName = "9) Riesgo y gestion")]
        public double ParcialEnR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mover stop a breakeven", Order = 6, GroupName = "9) Riesgo y gestion")]
        public bool UsarBreakeven { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Breakeven desde (R)", Order = 7, GroupName = "9) Riesgo y gestion",
                 Description = "Medido: mover a BE en 1R con objetivo lejano destruye el PF. "
                             + "Por eso el valor por defecto es 2.0, no 1.0.")]
        public double BeDesdeR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Trailing stop", Order = 8, GroupName = "9) Riesgo y gestion")]
        public bool UsarTrailing { get; set; }

        [NinjaScriptProperty] [Range(1, 400)]
        [Display(Name = "Distancia del trailing (ticks)", Order = 9, GroupName = "9) Riesgo y gestion")]
        public int TrailingTicks { get; set; }
        #endregion

        #region 10) Limites diarios
        [NinjaScriptProperty] [Range(0, 100000)]
        [Display(Name = "Perdida maxima al dia (USD)", Order = 1, GroupName = "10) Limites diarios",
                 Description = "0 = sin limite. Al tocarlo la estrategia deja de operar hasta manana.")]
        public double PerdidaMaximaDia { get; set; }

        [NinjaScriptProperty] [Range(0, 100000)]
        [Display(Name = "Objetivo diario (USD)", Order = 2, GroupName = "10) Limites diarios",
                 Description = "0 = sin objetivo. Al tocarlo deja de operar hasta manana.")]
        public double ObjetivoDia { get; set; }
        #endregion

        #region 11) Diagnostico
        [NinjaScriptProperty]
        [Display(Name = "Imprimir embudo en Output", Order = 1, GroupName = "11) Diagnostico")]
        public bool ImprimirEmbudo { get; set; }
        #endregion
    }
}
