// ===================================================================
//  FOOTPRINT FVG - NinjaTrader 8 (NinjaScript / C#)
//  Estrategia backtesteable derivada del indicador VolumeProfileSMC.
//
//  POR QUE ESTE ES DISTINTO A TODO LO ANTERIOR
//  Los sistemas que probamos (barrido->MSS, AMD, ORB, reversion a VWAP)
//  se apoyaban en PRECIO: pivotes, rangos, bandas. Todos aterrizaron en
//  PF ~1.0 sobre el ano completo de ES. La medicion de deriva explico
//  por que: la sesion RTH convierte solo el 1.4% de su rango diario en
//  movimiento neto. No hay direccion que extraer del precio solo.
//
//  Este parte de otra informacion: el FOOTPRINT. En concreto el POC de
//  cada vela, es decir a que precio DENTRO de la vela se negocio mas
//  volumen. Eso no se puede reconstruir desde OHLCV ni desde ticks de
//  trades agregados: solo lo dan las barras Volumetric. Es la unica
//  fuente de informacion que no hemos podido medir todavia.
//
//  LA TESIS (absorcion, leida por posicion del POC)
//    Vela alcista util : el POC esta en la MITAD BAJA. El volumen se
//                        concentro abajo y el precio subio igual -> el
//                        vendedor agresivo fue absorbido por un pasivo.
//    Vela bajista util : el POC esta en la MITAD ALTA, espejo.
//  Coincide con lo que apuntaban los ticks reales: en la vela clave el
//  flujo agresivo debe ir CONTRA la entrada, no a favor.
//
//  LOS TRES PASOS
//    1) ESTRUCTURA  FVG en 15m (hueco de 3 velas), queda activo hasta
//                   que el precio lo invalida
//    2) UBICACION   el POC de la vela de ejecucion cae DENTRO del FVG
//    3) FLUJO       el delta confirma y el POC esta en la mitad correcta
//
//  REQUISITO: barras Volumetric = licencia Lifetime o Order Flow+.
//  Sin eso AddVolumetric falla en tiempo de ejecucion.
//
//  DOM (Level 2): incluido como confirmacion OPCIONAL y SOLO EN VIVO.
//  NinjaTrader no guarda Level 2 historico y OnMarketDepth() no se
//  dispara en backtest, asi que en el Probador este filtro es inerte
//  por diseno. Medido aparte: el flujo de ordenes no predice precio a
//  1-60 minutos (acierta el signo 49.4-50.3%), asi que el DOM aqui es
//  para ejecucion en vivo, no para buscar ventaja.
//
//  ANTI-REPINTADO: Calculate.OnBarClose. El FVG se fija con velas de 15m
//  CERRADAS y nunca se recalcula hacia atras.
//
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
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.BarsTypes;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>Que se le exige a la posicion del POC dentro de la vela.</summary>
    public enum FpPocMode
    {
        Absorcion,   // POC en la mitad CONTRARIA al trade (lo que sugiere la data)
        Momentum,    // POC en la mitad A FAVOR del trade (lectura clasica)
        Ninguno      // no se mira la posicion del POC
    }

    /// <summary>Como se calcula el objetivo.</summary>
    public enum FpTargetMode
    {
        MultiploDeR,
        LadoOpuestoDelFvg
    }

    public class FootprintFvgStrategy : Strategy
    {
        // ---------- indices de series ----------
        private int IdxHtf = 1;   // 15m: estructura (FVG)
        private int IdxVol = 2;   // Volumetric: footprint

        // ---------- FVG activos ----------
        private double fvgAlcTop = double.NaN, fvgAlcBot = double.NaN;
        private double fvgBajTop = double.NaN, fvgBajBot = double.NaN;
        private int    fvgAlcBar = -1, fvgBajBar = -1;

        // ---------- footprint de la vela en curso ----------
        private double pocPrecio = double.NaN;
        private double pocRel    = double.NaN;   // 0 = POC en el minimo, 1 = en el maximo
        private double barDelta  = 0;
        private double barVolTot = 0;

        // ---------- DOM (solo en vivo) ----------
        private double domBid = 0, domAsk = 0;
        private bool   domVivo = false;

        // ---------- posicion ----------
        private double posEntry, posSL, posTP;
        private bool   beHecho;
        private int    ultimaEntradaBar = -1;

        // ---------- control diario ----------
        private double dayStartCum = 0;
        private int    opsHoy = 0;
        private bool   diaBloqueado = false;
        private DateTime diaActual = DateTime.MinValue;

        // ---------- embudo ----------
        private int cntFvg, cntPocDentro, cntSenal, cntIn;
        private int cntNoDelta, cntNoPoc, cntNoSesion, cntNoRiesgo, cntNoDia, cntNoDom;
        private bool embudoImpreso = false;

        private ATR atrInd;

        // ===============================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "FVG de 15m + POC de la vela dentro del hueco + delta y absorcion "
                            + "leida por posicion del POC. Requiere barras Volumetric.";
                Name        = "FootprintFvgStrategy";

                Calculate                                 = Calculate.OnBarClose;
                EntriesPerDirection                       = 1;
                EntryHandling                             = EntryHandling.AllEntries;
                StopTargetHandling                        = StopTargetHandling.PerEntryExecution;
                IsExitOnSessionCloseStrategy              = true;
                ExitOnSessionCloseSeconds                 = 60;
                IsFillLimitOnTouch                        = false;
                MaximumBarsLookBack                       = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution                       = OrderFillResolution.Standard;
                Slippage                                  = 1;
                StartBehavior                             = StartBehavior.WaitUntilFlat;
                TimeInForce                               = TimeInForce.Day;
                RealtimeErrorHandling                     = RealtimeErrorHandling.StopCancelClose;
                BarsRequiredToTrade                       = 20;
                IsInstantiatedOnEachOptimizationIteration = true;

                // 1) Estructura
                MinutosHtf     = 15;
                FvgMinTicks    = 4;
                FvgMaxVelas    = 40;

                // 2) Footprint
                MinutosVol     = 1;
                TicksPorNivel  = 1;
                ModoPoc        = FpPocMode.Absorcion;
                PocUmbral      = 0.40;    // mitad baja = pocRel <= 0.40 para largos
                DeltaMinRatio  = 0.10;    // |delta| / volumen de la vela
                DeltaMinAbs    = 0;       // 0 = solo se usa el ratio

                // 3) Sesion (hora ET)
                UsarSesion     = true;
                SesionIni      = 930;
                SesionFin      = 1500;
                CierreET       = 1600;
                OffsetHorasET  = 0;

                // 4) Riesgo
                ModoObjetivo   = FpTargetMode.MultiploDeR;
                MultiploR      = 2.0;
                SlBufTicks     = 2;
                Contratos      = 1;
                MinRPuntos     = 2.0;
                MaxRPuntos     = 40.0;

                // 5) Limites (prop firm)
                MaxOpsDia      = 3;
                StopDiarioUsd  = 800;
                ObjetivoDiaUsd = 0;
                UsarBreakeven  = false;
                BeAtR          = 1.5;

                // 6) DOM (solo en vivo)
                UsarDom        = false;
                DomRatioMin    = 1.5;

                // 7) Visual
                MostrarVisuales = true;
            }
            else if (State == State.Configure)
            {
                IdxHtf = 1;
                AddDataSeries(BarsPeriodType.Minute, MinutosHtf);

                IdxVol = 2;
                AddVolumetric(null, BarsPeriodType.Minute, MinutosVol,
                              VolumetricDeltaType.BidAsk, TicksPorNivel);
            }
            else if (State == State.DataLoaded)
            {
                atrInd = ATR(14);
                Reset();
            }
            else if (State == State.Realtime)
            {
                domVivo = true;
            }
            else if (State == State.Terminated)
            {
                ImprimirEmbudo();
            }
        }

        private void Reset()
        {
            fvgAlcTop = fvgAlcBot = fvgBajTop = fvgBajBot = double.NaN;
            fvgAlcBar = fvgBajBar = -1;
            pocPrecio = pocRel = double.NaN;
            barDelta = barVolTot = 0;
            domBid = domAsk = 0; domVivo = false;
            posEntry = posSL = posTP = 0; beHecho = false; ultimaEntradaBar = -1;
            dayStartCum = 0; opsHoy = 0; diaBloqueado = false; diaActual = DateTime.MinValue;
            cntFvg = cntPocDentro = cntSenal = cntIn = 0;
            cntNoDelta = cntNoPoc = cntNoSesion = cntNoRiesgo = cntNoDia = cntNoDom = 0;
            embudoImpreso = false;
        }

        // ===============================================================
        //  DOM - SOLO EN VIVO. No se dispara en backtest, por diseno de
        //  NinjaTrader: el Level 2 no se guarda en el historico.
        // ===============================================================
        protected override void OnMarketDepth(MarketDepthEventArgs e)
        {
            if (e.Position != 0) return;              // solo el mejor nivel
            if (e.MarketDataType == MarketDataType.Ask) domAsk = e.Volume;
            else if (e.MarketDataType == MarketDataType.Bid) domBid = e.Volume;
        }

        /// <summary>Presion del libro en el mejor nivel. Devuelve true si no aplica.</summary>
        private bool DomConfirma(int dir)
        {
            if (!UsarDom) return true;
            if (!domVivo || domBid <= 0 || domAsk <= 0) return true;  // backtest: inerte
            double ratio = dir > 0 ? domBid / domAsk : domAsk / domBid;
            return ratio >= DomRatioMin;
        }

        // ===============================================================
        protected override void OnBarUpdate()
        {
            // ---------- 15m: mapear los FVG con velas CERRADAS ----------
            if (BarsInProgress == IdxHtf)
            {
                ActualizarFvg();
                return;
            }

            // ---------- Volumetric: leer el footprint de la vela ----------
            if (BarsInProgress == IdxVol)
            {
                LeerFootprint();
                return;
            }

            if (BarsInProgress != 0) return;

            // ============ SERIE PRIMARIA ============
            if (CurrentBar < BarsRequiredToTrade) return;
            if (CurrentBars[IdxHtf] < 4 || CurrentBars[IdxVol] < 2) return;
            double atr = atrInd[0];
            if (atr <= 0) return;

            DateTime et = Time[0].AddHours(OffsetHorasET);
            int hm = et.Hour * 100 + et.Minute;

            // nuevo dia de trading
            DateTime dia = hm >= 1800 ? et.Date.AddDays(1) : et.Date;
            if (dia != diaActual)
            {
                diaActual = dia;
                opsHoy = 0; diaBloqueado = false;
                dayStartCum = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
            }

            ControlDiario();

            // cierre forzado
            if (Position.MarketPosition != MarketPosition.Flat && hm >= CierreET)
            {
                if (Position.MarketPosition == MarketPosition.Long) ExitLong();
                else                                                ExitShort();
            }

            if (UsarBreakeven) GestionBe(Close[0]);
            if (CurrentBar >= Bars.Count - 2) ImprimirEmbudo();

            // caducar FVG viejos
            if (fvgAlcBar >= 0 && CurrentBars[IdxHtf] - fvgAlcBar > FvgMaxVelas) InvalidarAlc();
            if (fvgBajBar >= 0 && CurrentBars[IdxHtf] - fvgBajBar > FvgMaxVelas) InvalidarBaj();

            if (Position.MarketPosition != MarketPosition.Flat) return;
            if (CurrentBar <= ultimaEntradaBar) return;
            if (double.IsNaN(pocPrecio) || barVolTot <= 0) return;

            bool enSesion = !UsarSesion || (hm >= SesionIni && hm < SesionFin);
            double dRatio = barDelta / Math.Max(barVolTot, 1.0);

            // ---------- LARGO: POC dentro del FVG alcista ----------
            if (!double.IsNaN(fvgAlcBot) && pocPrecio >= fvgAlcBot && pocPrecio <= fvgAlcTop)
            {
                cntPocDentro++;
                if (Evalua(1, dRatio, enSesion, atr)) return;
            }

            // ---------- CORTO: POC dentro del FVG bajista ----------
            if (!double.IsNaN(fvgBajTop) && pocPrecio >= fvgBajBot && pocPrecio <= fvgBajTop)
            {
                cntPocDentro++;
                if (Evalua(-1, dRatio, enSesion, atr)) return;
            }
        }

        /// <summary>Aplica los filtros y entra. Devuelve true si abrio posicion.</summary>
        private bool Evalua(int dir, double dRatio, bool enSesion, double atr)
        {
            // --- flujo: el delta debe confirmar la direccion ---
            double dFav = dir > 0 ? dRatio : -dRatio;
            bool deltaOK = dFav >= DeltaMinRatio
                           && (DeltaMinAbs <= 0 || Math.Abs(barDelta) >= DeltaMinAbs);
            if (!deltaOK) { cntNoDelta++; return false; }

            // --- posicion del POC dentro de la vela ---
            bool pocOK = true;
            if (ModoPoc == FpPocMode.Absorcion)
                pocOK = dir > 0 ? pocRel <= PocUmbral : pocRel >= (1.0 - PocUmbral);
            else if (ModoPoc == FpPocMode.Momentum)
                pocOK = dir > 0 ? pocRel >= (1.0 - PocUmbral) : pocRel <= PocUmbral;
            if (!pocOK) { cntNoPoc++; return false; }

            cntSenal++;

            if (!enSesion)   { cntNoSesion++; return false; }
            if (!DomConfirma(dir)) { cntNoDom++; return false; }
            if (diaBloqueado || (MaxOpsDia > 0 && opsHoy >= MaxOpsDia)) { cntNoDia++; return false; }

            // --- riesgo: el stop va al borde lejano del FVG ---
            double e  = Close[0];
            double sl = dir > 0 ? fvgAlcBot - SlBufTicks * TickSize
                                : fvgBajTop + SlBufTicks * TickSize;
            double R  = Math.Abs(e - sl);
            if (R < MinRPuntos || R > MaxRPuntos) { cntNoRiesgo++; return false; }
            if ((dir > 0 && sl >= e) || (dir < 0 && sl <= e)) { cntNoRiesgo++; return false; }

            double tp;
            if (ModoObjetivo == FpTargetMode.LadoOpuestoDelFvg)
                tp = dir > 0 ? fvgAlcTop + R : fvgBajBot - R;
            else
                tp = dir > 0 ? e + MultiploR * R : e - MultiploR * R;
            if ((dir > 0 && tp <= e) || (dir < 0 && tp >= e)) { cntNoRiesgo++; return false; }

            posEntry = e; posSL = sl; posTP = tp; beHecho = false;
            SetStopLoss("FP", CalculationMode.Price, sl, false);
            SetProfitTarget("FP", CalculationMode.Price, tp);
            if (dir > 0) EnterLong(Contratos, "FP"); else EnterShort(Contratos, "FP");

            opsHoy++; cntIn++; ultimaEntradaBar = CurrentBar;
            if (dir > 0) InvalidarAlc(); else InvalidarBaj();   // el FVG se consume

            if (MostrarVisuales)
            {
                if (dir > 0) Draw.ArrowUp(this, "eL" + CurrentBar, false, 0, Low[0] - 4 * TickSize, Brushes.Lime);
                else         Draw.ArrowDown(this, "eS" + CurrentBar, false, 0, High[0] + 4 * TickSize, Brushes.Red);
            }
            return true;
        }

        // ===============================================================
        //  ESTRUCTURA: FVG de 15m
        // ===============================================================
        private void ActualizarFvg()
        {
            if (CurrentBars[IdxHtf] < 3) return;
            double minGap = FvgMinTicks * TickSize;

            // invalidacion: el precio cierra al otro lado del hueco
            if (!double.IsNaN(fvgAlcBot) && Closes[IdxHtf][0] < fvgAlcBot) InvalidarAlc();
            if (!double.IsNaN(fvgBajTop) && Closes[IdxHtf][0] > fvgBajTop) InvalidarBaj();

            // FVG alcista: el minimo de la vela actual queda por encima del
            // maximo de la de hace dos. Velas CERRADAS: no repinta.
            double gapAlc = Lows[IdxHtf][0] - Highs[IdxHtf][2];
            if (gapAlc >= minGap)
            {
                fvgAlcTop = Lows[IdxHtf][0];
                fvgAlcBot = Highs[IdxHtf][2];
                fvgAlcBar = CurrentBars[IdxHtf];
                cntFvg++;
                if (MostrarVisuales)
                    Draw.Rectangle(this, "fvgA" + fvgAlcBar, false, Times[IdxHtf][2], fvgAlcBot,
                                   Times[IdxHtf][0], fvgAlcTop, Brushes.Transparent, Brushes.SeaGreen, 15);
            }

            double gapBaj = Lows[IdxHtf][2] - Highs[IdxHtf][0];
            if (gapBaj >= minGap)
            {
                fvgBajTop = Lows[IdxHtf][2];
                fvgBajBot = Highs[IdxHtf][0];
                fvgBajBar = CurrentBars[IdxHtf];
                cntFvg++;
                if (MostrarVisuales)
                    Draw.Rectangle(this, "fvgB" + fvgBajBar, false, Times[IdxHtf][2], fvgBajBot,
                                   Times[IdxHtf][0], fvgBajTop, Brushes.Transparent, Brushes.IndianRed, 15);
            }
        }

        private void InvalidarAlc() { fvgAlcTop = fvgAlcBot = double.NaN; fvgAlcBar = -1; }
        private void InvalidarBaj() { fvgBajTop = fvgBajBot = double.NaN; fvgBajBar = -1; }

        // ===============================================================
        //  FOOTPRINT: POC y delta de la vela Volumetric cerrada
        // ===============================================================
        private void LeerFootprint()
        {
            pocPrecio = double.NaN; pocRel = double.NaN;
            barDelta = 0; barVolTot = 0;
            if (CurrentBars[IdxVol] < 0) return;

            try
            {
                VolumetricBarsType vb = BarsArray[IdxVol].BarsType as VolumetricBarsType;
                if (vb == null) return;
                var v = vb.Volumes[CurrentBars[IdxVol]];
                if (v == null) return;

                barDelta  = v.BarDelta;
                // El volumen total se toma de la serie, no del objeto volumetrico:
                // BarDelta y GetTotalVolumeForPrice estan comprobados, TotalVolume no.
                barVolTot = Volumes[IdxVol][0];

                double lo = Lows[IdxVol][0], hi = Highs[IdxVol][0];
                if (hi <= lo) return;
                // Escaneo del POC: el precio con mas volumen DENTRO de la vela.
                // Esto es lo que ninguna otra fuente de datos nos daba.
                long maxVol = 0; double poc = lo;
                int pasos = (int)Math.Round((hi - lo) / TickSize) + 1;
                if (pasos > 2000) return;              // seguro contra velas anomalas
                for (int i = 0; i < pasos; i++)
                {
                    double p = lo + i * TickSize;
                    long vol = v.GetTotalVolumeForPrice(p);
                    if (vol > maxVol) { maxVol = vol; poc = p; }
                }
                if (maxVol <= 0) return;
                pocPrecio = poc;
                pocRel    = (poc - lo) / (hi - lo);    // 0 = en el minimo, 1 = en el maximo
            }
            catch (Exception ex)
            {
                Print("Volumetric no disponible (necesitas Lifetime u Order Flow+): " + ex.Message);
            }
        }

        // ===============================================================
        //  GESTION
        // ===============================================================
        private void GestionBe(double px)
        {
            if (beHecho || Position.MarketPosition == MarketPosition.Flat) return;
            if (posEntry <= 0 || posSL <= 0) return;
            double ent = Position.AveragePrice > 0 ? Position.AveragePrice : posEntry;
            if (Position.MarketPosition == MarketPosition.Long)
            {
                if (ent > posSL && px >= ent + BeAtR * (ent - posSL))
                { beHecho = true; SetStopLoss("FP", CalculationMode.Price, ent, false); }
            }
            else
            {
                if (ent < posSL && px <= ent - BeAtR * (posSL - ent))
                { beHecho = true; SetStopLoss("FP", CalculationMode.Price, ent, false); }
            }
        }

        private void ControlDiario()
        {
            double real = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit - dayStartCum;
            double abierto = Position.MarketPosition == MarketPosition.Flat
                           ? 0 : Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
            double pnl = real + abierto;
            bool porPerdida  = StopDiarioUsd  > 0 && pnl <= -StopDiarioUsd;
            bool porGanancia = ObjetivoDiaUsd > 0 && pnl >=  ObjetivoDiaUsd;
            diaBloqueado = porPerdida || porGanancia;
            if (diaBloqueado && Position.MarketPosition != MarketPosition.Flat)
            {
                if (Position.MarketPosition == MarketPosition.Long) ExitLong();
                else                                                ExitShort();
            }
        }

        private void ImprimirEmbudo()
        {
            if (embudoImpreso || cntFvg == 0) return;
            embudoImpreso = true;
            Print("===== FOOTPRINT FVG - EMBUDO =====");
            Print("  Instrumento / TF        : " + Instrument.MasterInstrument.Name + " "
                  + BarsPeriod.Value + " " + BarsPeriod.BarsPeriodType);
            Print("  Estructura / Volumetric : " + MinutosHtf + "m / " + MinutosVol + "m");
            Print("  Modo POC                : " + ModoPoc + " (umbral " + PocUmbral + ")");
            Print("  FVG detectados          : " + cntFvg);
            Print("  POC dentro del FVG      : " + cntPocDentro);
            Print("  Descartados por delta   : " + cntNoDelta);
            Print("  Descartados por POC     : " + cntNoPoc);
            Print("  Senales validas         : " + cntSenal);
            Print("  Descartados por sesion  : " + cntNoSesion);
            Print("  Descartados por DOM     : " + cntNoDom);
            Print("  Descartados por riesgo  : " + cntNoRiesgo);
            Print("  Descartados por lim.dia : " + cntNoDia);
            Print("  ENTRADAS                : " + cntIn);
            Print("==================================");
        }

        // ===============================================================
        //  PARAMETROS
        // ===============================================================
        #region 1) Estructura
        [NinjaScriptProperty] [Range(1, 1440)]
        [Display(Name = "Minutos de la estructura (FVG)", Order = 1, GroupName = "1) Estructura")]
        public int MinutosHtf { get; set; }

        [NinjaScriptProperty] [Range(1, 400)]
        [Display(Name = "Tamano minimo del FVG (ticks)", Order = 2, GroupName = "1) Estructura")]
        public int FvgMinTicks { get; set; }

        [NinjaScriptProperty] [Range(1, 500)]
        [Display(Name = "Caducidad del FVG (velas de estructura)", Order = 3, GroupName = "1) Estructura")]
        public int FvgMaxVelas { get; set; }
        #endregion

        #region 2) Footprint
        [NinjaScriptProperty] [Range(1, 60)]
        [Display(Name = "Minutos de la barra Volumetric", Order = 1, GroupName = "2) Footprint")]
        public int MinutosVol { get; set; }

        [NinjaScriptProperty] [Range(1, 10)]
        [Display(Name = "Ticks por nivel de precio", Order = 2, GroupName = "2) Footprint")]
        public int TicksPorNivel { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Posicion del POC exigida", Order = 3, GroupName = "2) Footprint",
                 Description = "Absorcion: el POC en la mitad CONTRARIA al trade (el volumen se concentro "
                             + "donde el agresor empujaba y el precio no siguio). Momentum: el POC a favor. "
                             + "La data de ticks apunta a Absorcion, pero nunca se pudo medir con footprint real.")]
        public FpPocMode ModoPoc { get; set; }

        [NinjaScriptProperty] [Range(0.05, 0.5)]
        [Display(Name = "Umbral de posicion del POC", Order = 4, GroupName = "2) Footprint",
                 Description = "0.40 = el POC debe estar en el 40% inferior de la vela para un largo.")]
        public double PocUmbral { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Ratio minimo de delta", Order = 5, GroupName = "2) Footprint",
                 Description = "|delta| / volumen de la vela. Normalizado, asi que vale igual a las 09:30 que a las 14:00.")]
        public double DeltaMinRatio { get; set; }

        [NinjaScriptProperty] [Range(0, 100000)]
        [Display(Name = "Delta absoluto minimo (0 = off)", Order = 6, GroupName = "2) Footprint")]
        public int DeltaMinAbs { get; set; }
        #endregion

        #region 3) Sesion (hora ET)
        [NinjaScriptProperty]
        [Display(Name = "Filtrar por sesion", Order = 1, GroupName = "3) Sesion (hora ET)")]
        public bool UsarSesion { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Inicio (HHMM ET)", Order = 2, GroupName = "3) Sesion (hora ET)")]
        public int SesionIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Fin (HHMM ET)", Order = 3, GroupName = "3) Sesion (hora ET)")]
        public int SesionFin { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Cierre forzado (HHMM ET)", Order = 4, GroupName = "3) Sesion (hora ET)")]
        public int CierreET { get; set; }

        [NinjaScriptProperty] [Range(-12, 12)]
        [Display(Name = "Ajuste horario a ET (horas)", Order = 5, GroupName = "3) Sesion (hora ET)",
                 Description = "0 si tu NinjaTrader esta en hora del Este. +1 si esta en hora Central.")]
        public int OffsetHorasET { get; set; }
        #endregion

        #region 4) Riesgo
        [NinjaScriptProperty]
        [Display(Name = "Modo de objetivo", Order = 1, GroupName = "4) Riesgo")]
        public FpTargetMode ModoObjetivo { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Multiplo de R", Order = 2, GroupName = "4) Riesgo")]
        public double MultiploR { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Colchon del SL (ticks)", Order = 3, GroupName = "4) Riesgo")]
        public int SlBufTicks { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Contratos", Order = 4, GroupName = "4) Riesgo")]
        public int Contratos { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1000.0)]
        [Display(Name = "R minimo (puntos)", Order = 5, GroupName = "4) Riesgo",
                 Description = "Protege contra stops diminutos donde comision y slippage se comen la operacion.")]
        public double MinRPuntos { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1000.0)]
        [Display(Name = "R maximo (puntos)", Order = 6, GroupName = "4) Riesgo")]
        public double MaxRPuntos { get; set; }
        #endregion

        #region 5) Limites diarios
        [NinjaScriptProperty] [Range(0, 50)]
        [Display(Name = "Maximo de operaciones por dia", Order = 1, GroupName = "5) Limites diarios")]
        public int MaxOpsDia { get; set; }

        [NinjaScriptProperty] [Range(0, 1000000)]
        [Display(Name = "Stop de perdida diaria (USD, 0=off)", Order = 2, GroupName = "5) Limites diarios")]
        public double StopDiarioUsd { get; set; }

        [NinjaScriptProperty] [Range(0, 1000000)]
        [Display(Name = "Objetivo diario (USD, 0=off)", Order = 3, GroupName = "5) Limites diarios")]
        public double ObjetivoDiaUsd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mover SL a breakeven", Order = 4, GroupName = "5) Limites diarios",
                 Description = "OJO: medido sobre el ano completo, el breakeven a 1R hundio otra estrategia de "
                             + "PF 1.27 a 0.80. Si lo activas, ponlo cerca del objetivo, no a mitad de camino.")]
        public bool UsarBreakeven { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Breakeven a los (multiplo de R)", Order = 5, GroupName = "5) Limites diarios")]
        public double BeAtR { get; set; }
        #endregion

        #region 6) DOM (solo en vivo)
        [NinjaScriptProperty]
        [Display(Name = "Confirmar con el DOM", Order = 1, GroupName = "6) DOM (solo en vivo)",
                 Description = "INERTE EN BACKTEST: NinjaTrader no guarda Level 2 historico y OnMarketDepth() "
                             + "no se dispara en el Probador. Solo actua operando en vivo o en Market Replay.")]
        public bool UsarDom { get; set; }

        [NinjaScriptProperty] [Range(1.0, 10.0)]
        [Display(Name = "Ratio minimo del libro", Order = 2, GroupName = "6) DOM (solo en vivo)",
                 Description = "Para un largo: volumen del bid / volumen del ask en el mejor nivel.")]
        public double DomRatioMin { get; set; }
        #endregion

        #region 7) Visual
        [NinjaScriptProperty]
        [Display(Name = "Dibujar en el grafico", Order = 1, GroupName = "7) Visual")]
        public bool MostrarVisuales { get; set; }
        #endregion
    }
}
