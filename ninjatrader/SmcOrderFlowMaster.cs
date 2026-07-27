// ===================================================================
//  SMC ORDER FLOW MASTER  -  NinjaTrader 8 (NinjaScript / C#)
//  Puerto institucional de "SMC Confluence Master v2" (Pine Script v6)
//
//  QUE ES: el mismo sistema de SCORE que valido en TradingView
//  (barrido de liquidez + MSS/CHoCH + FVG + sesion + sesgo HTF + VWAP
//  + volumen + SMT + EQH/EQL), pero con la pieza que TradingView NO
//  puede dar: DELTA REAL (volumen comprador vs vendedor) calculado
//  tick a tick sobre TODO el historico, no solo las ultimas 2 semanas.
//
//  POR QUE NINJATRADER ES MEJOR PARA ESTO:
//  1) Delta real backtesteable: se reconstruye desde la serie de 1 tick
//     (regla del tick) o desde las series Ask/Bid (clasificacion real
//     BidAsk, estandar institucional) o desde barras Volumetric.
//  2) Granularidad intrabar real: las ordenes se envian a una serie de
//     1 tick, asi el stop y el target se llenan al precio y momento
//     correctos dentro de la vela (en TV eso es una aproximacion).
//  3) Walk Forward Optimization y Monte Carlo integrados en el
//     Strategy Analyzer: la herramienta correcta contra el sobreajuste.
//  4) Market Replay: repeticion tick a tick con bid/ask real.
//
//  ANTI-REPINTADO:
//  - Calculate = OnBarClose. Las senales solo se evaluan al cierre de
//    la vela de la serie primaria (equivale a barstate.isconfirmed).
//  - El sesgo HTF se lee de la vela HTF CERRADA (la serie secundaria
//    solo dispara OnBarUpdate en su cierre con OnBarClose).
//  - Los pivotes se confirman con retardo fijo (PivotLen barras) y
//    nunca se redibujan.
//  - El delta de cada vela se acumula desde la serie de 1 tick y se
//    cierra exactamente en el cierre de la vela primaria.
//
//  SERIES DE DATOS (indices):
//    0            = serie primaria (el grafico: 1m, 5m, 15m...)
//    IdxTick      = 1 tick del mismo instrumento (delta + llenado real)
//    IdxHtf       = temporalidad mayor (sesgo EMA)
//    IdxSmt       = instrumento correlacionado (NQ) si SMT esta activo
//    IdxAsk/IdxBid= 1 tick Ask/Bid si el modo de delta es BidAsk
//    IdxVol       = barras Volumetric si el modo de delta es Volumetric
//
//  IMPORTANTE: al usar AddDataSeries(), el "Order Fill Resolution" del
//  Strategy Analyzer queda inhabilitado. Por eso TODAS las ordenes se
//  envian a la serie de 1 tick (IdxTick). Esa es la forma correcta y
//  documentada de obtener llenados intrabar en scripts multi-serie.
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
    /// <summary>Modo de calculo del stop y del objetivo.</summary>
    public enum SmcRiskMode
    {
        AtrEstructura,
        UsdFijo
    }

    /// <summary>De donde sale el volumen comprador / vendedor.</summary>
    public enum SmcDeltaSource
    {
        ReglaDelTick,   // 1 tick + uptick/downtick. Funciona con cualquier feed.
        BidAsk,         // 1 tick + series Ask/Bid. Estandar institucional.
        Volumetric      // barras Volumetric. Requiere licencia Lifetime / Order Flow+.
    }

    public class SmcOrderFlowMaster : Strategy
    {
        // ---------- indices de series ----------
        private int IdxTick = 1;
        private int IdxHtf  = 2;
        private int IdxSmt  = -1;
        private int IdxAsk  = -1;
        private int IdxBid  = -1;
        private int IdxVol  = -1;

        // ---------- indicadores cacheados ----------
        private ATR   atrInd;
        private SMA   volAvg;
        private EMA   emaHtf;

        // ---------- motor de order flow ----------
        private double accBuy, accSell;        // acumulador de la vela en curso
        private double barBuy, barSell;        // delta cerrado de la ultima vela primaria
        private double lastTickPx  = 0;
        private bool   lastTickBuy = true;
        private double curAsk = 0, curBid = 0;
        private double cvd = 0;                // delta acumulado de la sesion
        private Series<double> cvdSeries;      // CVD por vela primaria (para la absorcion)

        // ---------- VWAP de sesion (calculado a mano, sin licencia) ----------
        private double vwapPv = 0, vwapVol = 0, vwapVal = 0;

        // ---------- pivotes / niveles ----------
        private double lastPH = double.NaN, prevPH = double.NaN;
        private double lastPL = double.NaN, prevPL = double.NaN;
        private int    lastPHBar = -1, lastPLBar = -1;
        private double minorPH = double.NaN, minorPL = double.NaN;
        private double corrPH  = double.NaN, corrPL  = double.NaN;

        // ---------- FVG ----------
        private double bullFvgTop = double.NaN, bullFvgBot = double.NaN;
        private double bearFvgTop = double.NaN, bearFvgBot = double.NaN;
        private int    bullFvgBar = -1, bearFvgBar = -1;

        // ---------- maquina de setup: barrido alcista ----------
        private int    swBullBar = -1;
        private double swBullExt, swBullMssLvl;
        private double swBullDeltaRatio;
        private bool   swBullVol, swBullSmt, swBullEq, swBullMss, swBullAbsorp, swBullMssDelta;
        private int    swBullVolPts, swBullMssVolPts;

        // ---------- maquina de setup: barrido bajista ----------
        private int    swBearBar = -1;
        private double swBearExt, swBearMssLvl;
        private double swBearDeltaRatio;
        private bool   swBearVol, swBearSmt, swBearEq, swBearMss, swBearAbsorp, swBearMssDelta;
        private int    swBearVolPts, swBearMssVolPts;

        // ---------- gestion de la posicion ----------
        private double posEntry, posSL, posTP1, posTP2;
        private bool   beDone;
        private int    lastScore;

        // ---------- embudo de diagnostico ----------
        private int cntSweep, cntMss, cntIn;
        private bool embudoImpreso = false;
        private int cntNoSes, cntNoScore, cntNoOf, cntNoRisk, cntNoDia;

        // ---------- control diario ----------
        private int    sessionStartBar = -1;   // para no comparar CVD entre sesiones
        private int    lastEntryBar    = -1;   // evita reenviar la entrada si aun no lleno
        private double dayStartCum = 0;
        private int    tradesToday = 0;
        private bool   dayBlocked  = false;

        // ===============================================================
        //  ESTADO
        // ===============================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "SMC + Order Flow institucional. Barrido de liquidez, MSS/CHoCH, FVG, "
                            + "sesgo HTF, VWAP de sesion, SMT y delta real comprador/vendedor.";
                Name        = "SmcOrderFlowMaster";

                // OnBarClose = anti-repintado. La serie de 1 tick igual dispara
                // en cada tick, asi que no perdemos precision de gestion.
                Calculate                                   = Calculate.OnBarClose;
                EntriesPerDirection                         = 2;
                EntryHandling                               = EntryHandling.UniqueEntries;
                StopTargetHandling                          = StopTargetHandling.PerEntryExecution;
                IsExitOnSessionCloseStrategy                = true;
                ExitOnSessionCloseSeconds                   = 60;
                IsFillLimitOnTouch                          = false;
                MaximumBarsLookBack                         = MaximumBarsLookBack.Infinite;
                OrderFillResolution                         = OrderFillResolution.Standard; // multi-serie: se usa IdxTick
                Slippage                                    = 1;
                StartBehavior                               = StartBehavior.WaitUntilFlat;
                TimeInForce                                 = TimeInForce.Day;
                TraceOrders                                 = false;
                RealtimeErrorHandling                       = RealtimeErrorHandling.StopCancelClose;
                BarsRequiredToTrade                         = 20;
                // NO poner esto en false: esta estrategia guarda mucho estado
                // (pivotes, CVD, setups, contadores) y al reutilizar el objeto
                // ese estado se arrastraria de una iteracion de optimizacion a
                // la siguiente, contaminando el Walk Forward. Va en true aunque
                // cueste algo de velocidad.
                IsInstantiatedOnEachOptimizationIteration   = true;

                // ---- 1) Sesion ----
                UseSession   = true;
                SesStart     = 730;
                // 1100 y no 1400: sobre el ANO COMPLETO de velas de ES, la
                // franja 1300-1500 CT dio PF 0.62 con -13,319 USD en 162
                // senales. Cortar en 1100 sube el conjunto de PF 0.87 a 1.01.
                // Ademas el movimiento esta en la manana (rango medio 3.4-4.0
                // pts de 0800 a 1000 CT frente a 2.5-2.7 por la tarde).
                // Si quieres tu ventana original, pon 1400.
                SesEnd       = 1100;
                UseLunchBlock= true;
                LunchStart   = 1130;
                LunchEnd     = 1300;
                CtOffsetHours= 0;

                // ---- 2) Sesgo ----
                UseHtf       = true;
                HtfMinutes   = 60;
                HtfEmaPeriod = 50;
                UseVwapFilter= false;

                // ---- 3) Barrido ----
                PivotLen     = 4;
                AtrPeriod    = 14;
                EqTolAtr     = 0.25;
                WickAtr      = 0.5;

                // ---- 4) SMT ----
                UseSmt         = true;
                SmtInstrument  = "NQ ##-##";

                // ---- 5) Volumen ----
                // El volumen pasa a CONFIRMAR POR PUNTOS, nunca a prohibir.
                // Medido sobre el ano completo de ES en 5m, por terciles de
                // volumen del barrido: bajo PF 0.95 / medio 1.08 / ALTO 1.43.
                // El tercil bajo no es catastrofico, asi que se pondera en vez
                // de filtrar. En 15m el efecto es ruido (1.00/0.87/1.18).
                UseVolume    = true;
                VolLen       = 20;
                VolMult      = 1.3;      // >= 1.3x la media -> +1 punto
                VolMult2     = 2.0;      // >= 2.0x la media -> +2 puntos
                UseMssVolume = true;     // +1 mas si el MSS tambien trae volumen
                VolMssMult   = 1.3;

                // ---- 5C) Order flow ----
                DeltaSrc          = SmcDeltaSource.ReglaDelTick;
                UseOrderFlow      = false;
                OrderFlowAsScore  = false;
                OfMinRatio        = 0.0;
                UseAbsorption     = true;
                UseMssDelta       = false;
                OfMssMinRatio     = 0.15;

                // ---- 6) FVG ----
                RequireFvg   = false;
                FvgMinAtr    = 0.15;

                // ---- 7) MSS ----
                RequireMss   = true;
                MssPivotLen  = 2;

                // ---- 8) Senal y riesgo ----
                WindowBars   = 7;
                MinScore     = 5;
                SlBufAtr     = 0.5;
                Tp1R         = 1.5;
                Tp2R         = 3.0;

                RiskMode     = SmcRiskMode.AtrEstructura;
                Contracts    = 1;
                MaxLossUsd   = 600;
                TargetUsd    = 1500;
                FitStructural= true;

                // ---- 8D) Limite diario ----
                DailyLossLimit    = 0;
                DailyProfitTarget = 0;
                MaxTradesPerDay   = 0;

                // ---- 9) Ejecucion ----
                UseBreakeven      = true;
                BeAtR             = 1.5;
                CloseAtSessionEnd = true;

                // ---- 0) Rendimiento ----
                UsarSerieTick = true;

                // ---- 10) Visual ----
                ShowVisuals   = true;
                ShowDashboard = true;

                AddPlot(Brushes.Orange, "VWAP");
            }
            else if (State == State.Configure)
            {
                // 1) Serie de 1 tick: motor de delta + granularidad de llenado.
                //    En MODO RAPIDO no se carga: un ano de ticks de ES son
                //    cientos de millones de registros y el backtest no termina.
                int next = 1;
                if (UsarSerieTick)
                {
                    IdxTick = next++;
                    AddDataSeries(BarsPeriodType.Tick, 1);
                }
                else IdxTick = -1;

                // 2) Temporalidad mayor para el sesgo.
                IdxHtf = next++;
                AddDataSeries(BarsPeriodType.Minute, HtfMinutes);

                // 3) SMT (instrumento correlacionado, misma temporalidad).
                if (UseSmt)
                {
                    IdxSmt = next++;
                    AddDataSeries(SmtInstrument, BarsPeriod.BarsPeriodType, BarsPeriod.Value);
                }

                // 4) Ask/Bid para clasificacion real del volumen.
                if (DeltaSrc == SmcDeltaSource.BidAsk && UsarSerieTick)
                {
                    IdxAsk = next++;
                    AddDataSeries(null, BarsPeriodType.Tick, 1, MarketDataType.Ask);
                    IdxBid = next++;
                    AddDataSeries(null, BarsPeriodType.Tick, 1, MarketDataType.Bid);
                }

                // 5) Volumetric (requiere licencia Lifetime u Order Flow+).
                if (DeltaSrc == SmcDeltaSource.Volumetric)
                {
                    IdxVol = next++;
                    AddVolumetric(null, BarsPeriod.BarsPeriodType, BarsPeriod.Value,
                                  VolumetricDeltaType.BidAsk, 1);
                }
            }
            else if (State == State.DataLoaded)
            {
                atrInd = ATR(AtrPeriod);
                volAvg = SMA(Volume, VolLen);
                emaHtf = EMA(Closes[IdxHtf], HtfEmaPeriod);

                cvdSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                ResetEstado();
            }
            else if (State == State.Terminated)
            {
                ImprimirEmbudo();
            }
        }

        /// <summary>
        /// Embudo de diagnostico. Se llama desde la ultima vela Y desde
        /// State.Terminated: depender solo de Terminated resultaba fragil,
        /// segun el modo en que corras la estrategia no siempre se veia.
        /// </summary>
        private void ImprimirEmbudo()
        {
            if (embudoImpreso || cntSweep == 0) return;
            embudoImpreso = true;
            Print("===== SMC ORDER FLOW MASTER - EMBUDO =====");
            Print("  Instrumento / TF         : " + Instrument.MasterInstrument.Name
                  + " " + BarsPeriod.Value + " " + BarsPeriod.BarsPeriodType);
            Print("  Serie de 1 tick          : " + (UsarSerieTick ? "SI (order flow activo)"
                                                                   : "NO (modo rapido)"));
            Print("  Ventana                  : " + SesStart + "-" + SesEnd
                  + " (offset a CT " + CtOffsetHours + ")");
            Print("  Barridos detectados      : " + cntSweep);
            Print("  Confirmaron MSS          : " + cntMss);
            Print("  Descartados por sesion   : " + cntNoSes);
            Print("  Descartados por score    : " + cntNoScore);
            Print("  Descartados por flujo    : " + cntNoOf);
            Print("  Descartados por riesgo   : " + cntNoRisk);
            Print("  Descartados por lim.dia  : " + cntNoDia);
            Print("  ENTRADAS                 : " + cntIn);
            Print("==========================================");
        }

        /// <summary>
        /// Deja todo el estado en cero. Se llama al cargar los datos para que
        /// una reaplicacion en el mismo grafico no arrastre nada del run anterior.
        /// </summary>
        private void ResetEstado()
        {
            accBuy = accSell = barBuy = barSell = 0;
            lastTickPx = 0; lastTickBuy = true;
            curAsk = curBid = 0; cvd = 0;
            vwapPv = vwapVol = vwapVal = 0;

            lastPH = prevPH = lastPL = prevPL = double.NaN;
            lastPHBar = lastPLBar = -1;
            minorPH = minorPL = corrPH = corrPL = double.NaN;

            bullFvgTop = bullFvgBot = bearFvgTop = bearFvgBot = double.NaN;
            bullFvgBar = bearFvgBar = -1;

            swBullBar = swBearBar = -1;
            swBullExt = swBullMssLvl = swBullDeltaRatio = 0;
            swBearExt = swBearMssLvl = swBearDeltaRatio = 0;
            swBullVol = swBullSmt = swBullEq = swBullMss = swBullAbsorp = swBullMssDelta = false;
            swBullVolPts = swBullMssVolPts = swBearVolPts = swBearMssVolPts = 0;
            swBearVol = swBearSmt = swBearEq = swBearMss = swBearAbsorp = swBearMssDelta = false;

            posEntry = posSL = posTP1 = posTP2 = 0;
            beDone = false; lastScore = 0;

            cntSweep = cntMss = cntIn = 0;
            embudoImpreso = false;
            cntNoSes = cntNoScore = cntNoOf = cntNoRisk = cntNoDia = 0;

            sessionStartBar = -1; lastEntryBar = -1;
            dayStartCum = 0; tradesToday = 0; dayBlocked = false;
        }

        // ===============================================================
        //  BUCLE PRINCIPAL
        // ===============================================================
        protected override void OnBarUpdate()
        {
            // --- serie Ask / Bid: solo refrescan el mejor precio ---
            if (BarsInProgress == IdxAsk) { curAsk = Closes[IdxAsk][0]; return; }
            if (BarsInProgress == IdxBid) { curBid = Closes[IdxBid][0]; return; }

            // --- serie de 1 tick: motor de delta + gestion intrabar ---
            if (IdxTick > 0 && BarsInProgress == IdxTick)
            {
                AcumularDelta();
                GestionIntrabar();
                return;
            }

            // --- SMT: pivotes del instrumento correlacionado ---
            if (IdxSmt > 0 && BarsInProgress == IdxSmt)
            {
                ActualizarPivotesSmt();
                return;
            }

            // --- cualquier otra serie secundaria: solo alimenta indicadores ---
            if (BarsInProgress != 0) return;

            // ============ A PARTIR DE AQUI: SERIE PRIMARIA, VELA CERRADA ============
            // 0) Cerrar el delta de la vela que acaba de terminar. Se hace antes
            //    de cualquier "return" para que el acumulador no arrastre ticks
            //    de varias velas durante el calentamiento.
            CerrarDeltaDeVela();

            // 1) Nueva sesion: reiniciar VWAP, CVD y contadores del dia.
            if (Bars.IsFirstBarOfSession)
            {
                vwapPv = 0; vwapVol = 0;
                cvd = 0;
                sessionStartBar = CurrentBar;
                dayStartCum = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
                tradesToday = 0;
                dayBlocked  = false;
            }

            // 2) VWAP de sesion (calculado siempre, tambien en el calentamiento).
            double tp = (High[0] + Low[0] + Close[0]) / 3.0;
            vwapPv  += tp * Volume[0];
            vwapVol += Volume[0];
            vwapVal  = vwapVol > 0 ? vwapPv / vwapVol : Close[0];
            Values[0][0] = vwapVal;

            // 3) Historial de CVD por vela (lo necesita la deteccion de absorcion,
            //    por eso se escribe en TODAS las velas, no solo en las operables).
            double barDelta = barBuy - barSell;
            double barTot   = barBuy + barSell;
            double barRatio = barTot > 0 ? barDelta / barTot : 0.0;
            cvdSeries[0]    = cvd;

            // A partir de aqui hace falta historia suficiente para operar.
            if (CurrentBars[0] < Math.Max(BarsRequiredToTrade, PivotLen * 2 + 2)) return;
            if (IdxTick > 0 && CurrentBars[IdxTick] < 1) return;
            if (CurrentBars[IdxHtf] < HtfEmaPeriod + 1) return;

            double atr = atrInd[0];
            if (atr <= 0) return;

            // 4) Contexto.
            DateTime ct   = Time[0].AddHours(CtOffsetHours);
            bool inSesion = !UseSession || EnVentana(ct, SesStart, SesEnd);
            bool inLunch  = UseLunchBlock && EnVentana(ct, LunchStart, LunchEnd);
            bool sesionOK = inSesion && !inLunch;

            bool htfBull = Close[0] > emaHtf[0];
            bool htfBear = Close[0] < emaHtf[0];
            bool vwBull  = Close[0] > vwapVal;
            bool vwBear  = Close[0] < vwapVal;
            double volRat = volAvg[0] > 0 ? Volume[0] / volAvg[0] : 0.0;
            bool volOK    = volRat >= VolMult;
            int  volPts   = !UseVolume ? 0 : (volRat >= VolMult2 ? 2 : (volRat >= VolMult ? 1 : 0));

            // 5) Pivotes propios (confirmados con retardo, no repintan).
            ActualizarPivotes();

            // 6) FVG.
            ActualizarFvg(atr);

            // 7) Barrido de liquidez.
            double upperWick = High[0] - Math.Max(Open[0], Close[0]);
            double lowerWick = Math.Min(Open[0], Close[0]) - Low[0];

            bool sweepBull = !double.IsNaN(lastPL) && Low[0]  < lastPL && Close[0] > lastPL
                             && lowerWick >= WickAtr * atr;
            bool sweepBear = !double.IsNaN(lastPH) && High[0] > lastPH && Close[0] < lastPH
                             && upperWick >= WickAtr * atr;

            bool eqlTag = !double.IsNaN(lastPL) && !double.IsNaN(prevPL)
                          && Math.Abs(lastPL - prevPL) <= EqTolAtr * atr;
            bool eqhTag = !double.IsNaN(lastPH) && !double.IsNaN(prevPH)
                          && Math.Abs(lastPH - prevPH) <= EqTolAtr * atr;

            // 8) Expirar setups viejos.
            if (swBullBar >= 0 && (CurrentBar - swBullBar) > WindowBars) { swBullBar = -1; swBullMss = false; }
            if (swBearBar >= 0 && (CurrentBar - swBearBar) > WindowBars) { swBearBar = -1; swBearMss = false; }

            // 9) Registrar el barrido con TODA su huella de order flow.
            if (sweepBull || sweepBear) cntSweep++;
            if (sweepBull)
            {
                swBullBar        = CurrentBar;
                swBullExt        = Low[0];
                swBullMssLvl     = double.IsNaN(minorPH) ? High[0] : minorPH;
                swBullVol        = volOK;
                swBullVolPts     = volPts;
                swBullMssVolPts  = 0;
                swBullSmt        = SmtDisponible() && !double.IsNaN(corrPL) && Lows[IdxSmt][0] > corrPL;
                swBullEq         = eqlTag;
                swBullMss        = false;
                swBullMssDelta   = false;
                swBullDeltaRatio = barRatio;
                // Absorcion: el precio hace un minimo MAS BAJO que el swing
                // anterior, pero el CVD NO hace un minimo mas bajo -> el
                // vendedor empujo sin flujo detras. Firma clasica de trampa.
                // El CVD se reinicia en cada sesion, asi que solo tiene sentido
                // comparar contra un swing de la MISMA sesion. Sobre tus ticks
                // reales de ES esto afecta al 1% de los barridos, pero comparar
                // el CVD de hoy contra el de ayer no significa nada.
                swBullAbsorp = false;
                if (lastPLBar >= sessionStartBar && lastPLBar >= 0)
                {
                    int back = CurrentBar - lastPLBar;
                    if (back > 0 && back < CurrentBar)
                        swBullAbsorp = cvd > cvdSeries[back];
                }
                if (ShowVisuals)
                    Draw.TriangleUp(this, "swB" + CurrentBar, false, 0, Low[0] - 2 * TickSize, Brushes.LimeGreen);
            }

            if (sweepBear)
            {
                swBearBar        = CurrentBar;
                swBearExt        = High[0];
                swBearMssLvl     = double.IsNaN(minorPL) ? Low[0] : minorPL;
                swBearVol        = volOK;
                swBearVolPts     = volPts;
                swBearMssVolPts  = 0;
                swBearSmt        = SmtDisponible() && !double.IsNaN(corrPH) && Highs[IdxSmt][0] < corrPH;
                swBearEq         = eqhTag;
                swBearMss        = false;
                swBearMssDelta   = false;
                swBearDeltaRatio = barRatio;
                swBearAbsorp = false;
                if (lastPHBar >= sessionStartBar && lastPHBar >= 0)
                {
                    int back = CurrentBar - lastPHBar;
                    if (back > 0 && back < CurrentBar)
                        swBearAbsorp = cvd < cvdSeries[back];
                }
                if (ShowVisuals)
                    Draw.TriangleDown(this, "swS" + CurrentBar, false, 0, High[0] + 2 * TickSize, Brushes.Red);
            }

            // 10) MSS / CHoCH: rompe la estructura menor en contra del barrido.
            bool mssNewBull = false, mssNewBear = false;
            if (swBullBar >= 0 && !swBullMss && !double.IsNaN(swBullMssLvl) && Close[0] > swBullMssLvl)
            {
                swBullMss       = true;
                swBullMssVolPts = (UseVolume && UseMssVolume && volRat >= VolMssMult) ? 1 : 0;
                mssNewBull      = true;
                cntMss++;
                swBullMssDelta = barRatio >= OfMssMinRatio;
                if (ShowVisuals)
                    Draw.Text(this, "mssB" + CurrentBar, "MSS", 0, High[0] + 4 * TickSize, Brushes.LimeGreen);
            }
            if (swBearBar >= 0 && !swBearMss && !double.IsNaN(swBearMssLvl) && Close[0] < swBearMssLvl)
            {
                swBearMss       = true;
                swBearMssVolPts = (UseVolume && UseMssVolume && volRat >= VolMssMult) ? 1 : 0;
                mssNewBear      = true;
                cntMss++;
                swBearMssDelta = barRatio <= -OfMssMinRatio;
                if (ShowVisuals)
                    Draw.Text(this, "mssS" + CurrentBar, "MSS", 0, Low[0] - 4 * TickSize, Brushes.Red);
            }

            // 11) Order flow: confirmaciones del lado comprador / vendedor.
            bool ofBullYes = swBullDeltaRatio >=  OfMinRatio;
            bool ofBearYes = swBearDeltaRatio <= -OfMinRatio;

            // Sin serie de 1 tick (modo rapido) no hay delta que medir: los tres
            // filtros de order flow se desactivan solos, porque si no un delta de
            // cero los pondria en falso y bloquearia TODAS las senales.
            if (!UsarSerieTick && DeltaSrc != SmcDeltaSource.Volumetric)
            {
                ofBullYes = true; ofBearYes = true;
                swBullAbsorp = true; swBearAbsorp = true;
                swBullMssDelta = true; swBearMssDelta = true;
            }

            // 12) SCORE. Base 7 (identico al sistema validado en TradingView).
            //     Si OrderFlowAsScore esta activo, el flujo suma hasta 3 puntos
            //     mas (max 10) en vez de ser un requisito duro.
            bool smtAvail = UseSmt && SmtDisponible();

            int scoreBull = 2
                + (!UseHtf         || htfBull   ? 1 : 0)
                + (!UseVwapFilter  || vwBull    ? 1 : 0)
                + (UseVolume ? swBullVolPts + swBullMssVolPts : 1)
                + (!smtAvail       || swBullSmt ? 1 : 0)
                + (swBullEq ? 1 : 0);

            int scoreBear = 2
                + (!UseHtf         || htfBear   ? 1 : 0)
                + (!UseVwapFilter  || vwBear    ? 1 : 0)
                + (UseVolume ? swBearVolPts + swBearMssVolPts : 1)
                + (!smtAvail       || swBearSmt ? 1 : 0)
                + (swBearEq ? 1 : 0);

            if (OrderFlowAsScore)
            {
                scoreBull += (ofBullYes ? 1 : 0)
                           + (UseAbsorption && swBullAbsorp ? 1 : 0)
                           + (UseMssDelta   && swBullMssDelta ? 1 : 0);
                scoreBear += (ofBearYes ? 1 : 0)
                           + (UseAbsorption && swBearAbsorp ? 1 : 0)
                           + (UseMssDelta   && swBearMssDelta ? 1 : 0);
            }

            // Puerta dura de order flow (si no esta en modo score).
            // Los tres filtros son INDEPENDIENTES: se puede exigir solo la
            // absorcion, solo el delta del sweep, o cualquier combinacion.
            // Sobre los ticks reales de ES cada uno por separado supero a la
            // combinacion de ambos, asi que no los enciendas los dos a ciegas.
            bool ofBullOK = OrderFlowAsScore
                            || ((!UseOrderFlow  || ofBullYes)
                                && (!UseAbsorption || swBullAbsorp)
                                && (!UseMssDelta   || swBullMssDelta));
            bool ofBearOK = OrderFlowAsScore
                            || ((!UseOrderFlow  || ofBearYes)
                                && (!UseAbsorption || swBearAbsorp)
                                && (!UseMssDelta   || swBearMssDelta));

            // 13) Resto de condiciones.
            bool winBull = swBullBar >= 0 && (CurrentBar - swBullBar) <= WindowBars;
            bool winBear = swBearBar >= 0 && (CurrentBar - swBearBar) <= WindowBars;

            bool inBullFvg = !double.IsNaN(bullFvgBot) && Low[0]  <= bullFvgTop && Close[0] >= bullFvgBot;
            bool inBearFvg = !double.IsNaN(bearFvgTop) && High[0] >= bearFvgBot && Close[0] <= bearFvgTop;
            bool fvgBullOK = !RequireFvg || (inBullFvg && bullFvgBar >= swBullBar);
            bool fvgBearOK = !RequireFvg || (inBearFvg && bearFvgBar >= swBearBar);

            bool mssBullOK = !RequireMss || swBullMss;
            bool mssBearOK = !RequireMss || swBearMss;

            bool usdMode   = RiskMode == SmcRiskMode.UsdFijo;
            double pvQty   = Instrument.MasterInstrument.PointValue * Contracts;
            double riskPts = pvQty > 0 ? MaxLossUsd / pvQty : 0;
            double tgtPts  = pvQty > 0 ? TargetUsd  / pvQty : 0;

            double slStructBull = swBullExt - SlBufAtr * atr;
            double slStructBear = swBearExt + SlBufAtr * atr;
            bool riskOKBull = !usdMode || !FitStructural || (Close[0] - slStructBull) <= riskPts;
            bool riskOKBear = !usdMode || !FitStructural || (slStructBear - Close[0]) <= riskPts;

            // 14) Control diario (regla de prop firm).
            ActualizarControlDiario();

            bool gateBull = sesionOK && mssBullOK && fvgBullOK && riskOKBull && ofBullOK && !dayBlocked;
            bool gateBear = sesionOK && mssBearOK && fvgBearOK && riskOKBear && ofBearOK && !dayBlocked;

            bool buySig  = winBull && gateBull && scoreBull >= MinScore;
            bool sellSig = winBear && gateBear && scoreBear >= MinScore;

            // Embudo de diagnostico: en el momento en que el MSS confirma, se
            // anota QUE filtro mato el setup. Es lo que te dice si el sistema
            // no opera por la sesion, por el score o por el order flow.
            if (mssNewBull || mssNewBear)
            {
                bool sc = mssNewBull ? scoreBull >= MinScore : scoreBear >= MinScore;
                bool of = mssNewBull ? ofBullOK   : ofBearOK;
                bool rk = mssNewBull ? riskOKBull : riskOKBear;
                if      (!sesionOK)   cntNoSes++;
                else if (!sc)         cntNoScore++;
                else if (!of)         cntNoOf++;
                else if (!rk)         cntNoRisk++;
                else if (dayBlocked)  cntNoDia++;
            }

            // 15) Cierre por fin de sesion.
            if (CloseAtSessionEnd && Position.MarketPosition != MarketPosition.Flat && !sesionOK)
            {
                if (Position.MarketPosition == MarketPosition.Long) ExitLong();
                else                                               ExitShort();
            }

            // 16) Ejecucion.
            // La entrada es a mercado sobre la serie de 1 tick: se llena en el
            // tick siguiente, no en esta llamada. El guardia de CurrentBar evita
            // reenviarla si la vela cierra antes de que la posicion aparezca.
            if (Position.MarketPosition == MarketPosition.Flat && CurrentBar > lastEntryBar)
            {
                if (buySig)       AbrirLargo(atr, usdMode, riskPts, tgtPts, slStructBull, scoreBull);
                else if (sellSig) AbrirCorto(atr, usdMode, riskPts, tgtPts, slStructBear, scoreBear);
            }

            // 16b) Sin serie de 1 tick el breakeven se evalua al cierre de vela.
            if (IdxTick < 0) GestionBarra(Close[0]);

            // 16c) Embudo: se imprime al llegar a las ultimas velas, sin
            //      esperar a State.Terminated.
            if (CurrentBar >= Bars.Count - 2) ImprimirEmbudo();

            // 17) Panel.
            if (ShowDashboard && State == State.Realtime)
                PintarPanel(sesionOK, htfBull, vwBull, barRatio, scoreBull, scoreBear, winBull, winBear);
        }

        // ===============================================================
        //  MOTOR DE ORDER FLOW
        // ===============================================================

        /// <summary>Clasifica cada tick como compra o venta y lo acumula.</summary>
        private void AcumularDelta()
        {
            if (DeltaSrc == SmcDeltaSource.Volumetric) return; // se lee al cierre de vela

            double px = Closes[IdxTick][0];
            double v  = Volumes[IdxTick][0];
            if (v <= 0) { lastTickPx = px; return; }

            bool buy;
            if (DeltaSrc == SmcDeltaSource.BidAsk && curAsk > 0 && curBid > 0 && curAsk >= curBid)
            {
                // Clasificacion institucional: se ejecuto contra el ask
                // (agresor comprador) o contra el bid (agresor vendedor).
                if      (px >= curAsk) buy = true;
                else if (px <= curBid) buy = false;
                else                   buy = px > lastTickPx ? true : (px < lastTickPx ? false : lastTickBuy);
            }
            else
            {
                // Regla del tick: uptick = compra, downtick = venta,
                // sin cambio = se hereda la direccion anterior.
                buy = px > lastTickPx ? true : (px < lastTickPx ? false : lastTickBuy);
            }

            if (buy) accBuy += v; else accSell += v;

            lastTickPx  = px;
            lastTickBuy = buy;
            cvd        += buy ? v : -v;
        }

        /// <summary>
        /// Cierra el delta de la vela primaria que acaba de terminar.
        /// Se hace en el cierre de la vela (no por indice de barra) para no
        /// depender del orden de procesamiento entre series.
        /// </summary>
        private void CerrarDeltaDeVela()
        {
            if (DeltaSrc == SmcDeltaSource.Volumetric && IdxVol > 0 && CurrentBars[IdxVol] >= 0)
            {
                try
                {
                    NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType vbt =
                        BarsArray[IdxVol].BarsType as NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
                    if (vbt != null)
                    {
                        var vd  = vbt.Volumes[CurrentBars[IdxVol]];
                        barBuy  = vd.TotalBuyingVolume;
                        barSell = vd.TotalSellingVolume;
                        cvd    += vd.BarDelta;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Print("Volumetric no disponible (necesitas Lifetime u Order Flow+): " + ex.Message);
                }
                barBuy = 0; barSell = 0;
                return;
            }

            barBuy  = accBuy;
            barSell = accSell;
            accBuy  = 0;
            accSell = 0;
        }

        // ===============================================================
        //  PIVOTES
        // ===============================================================

        /// <summary>Pivote confirmado PivotLen barras despues. No repinta.</summary>
        private bool EsPivotAlto(int len)
        {
            double p = High[len];
            for (int i = 0; i <= len * 2; i++)
            {
                if (i == len) continue;
                if (High[i] >= p) return false;
            }
            return true;
        }

        private bool EsPivotBajo(int len)
        {
            double p = Low[len];
            for (int i = 0; i <= len * 2; i++)
            {
                if (i == len) continue;
                if (Low[i] <= p) return false;
            }
            return true;
        }

        private void ActualizarPivotes()
        {
            if (CurrentBar >= PivotLen * 2)
            {
                if (EsPivotAlto(PivotLen))
                {
                    prevPH = lastPH;
                    lastPH = High[PivotLen];
                    lastPHBar = CurrentBar - PivotLen;
                }
                if (EsPivotBajo(PivotLen))
                {
                    prevPL = lastPL;
                    lastPL = Low[PivotLen];
                    lastPLBar = CurrentBar - PivotLen;
                }
            }

            if (CurrentBar >= MssPivotLen * 2)
            {
                if (EsPivotAlto(MssPivotLen)) minorPH = High[MssPivotLen];
                if (EsPivotBajo(MssPivotLen)) minorPL = Low[MssPivotLen];
            }
        }

        private bool SmtDisponible()
        {
            return IdxSmt > 0 && CurrentBars[IdxSmt] >= PivotLen * 2 + 1;
        }

        private void ActualizarPivotesSmt()
        {
            int n = PivotLen;
            if (CurrentBars[IdxSmt] < n * 2) return;

            double ph = Highs[IdxSmt][n];
            bool okH = true;
            for (int i = 0; i <= n * 2; i++)
            {
                if (i == n) continue;
                if (Highs[IdxSmt][i] >= ph) { okH = false; break; }
            }
            if (okH) corrPH = ph;

            double pl = Lows[IdxSmt][n];
            bool okL = true;
            for (int i = 0; i <= n * 2; i++)
            {
                if (i == n) continue;
                if (Lows[IdxSmt][i] <= pl) { okL = false; break; }
            }
            if (okL) corrPL = pl;
        }

        // ===============================================================
        //  FVG
        // ===============================================================
        private void ActualizarFvg(double atr)
        {
            if (CurrentBar < 3) return;

            if (Low[0] > High[2] && (Low[0] - High[2]) >= FvgMinAtr * atr)
            {
                bullFvgTop = Low[0];
                bullFvgBot = High[2];
                bullFvgBar = CurrentBar;
                if (ShowVisuals)
                    Draw.Rectangle(this, "fvgB" + CurrentBar, false, 2, High[2], 0, Low[0],
                                   Brushes.Transparent, Brushes.Green, 12);
            }
            if (High[0] < Low[2] && (Low[2] - High[0]) >= FvgMinAtr * atr)
            {
                bearFvgTop = Low[2];
                bearFvgBot = High[0];
                bearFvgBar = CurrentBar;
                if (ShowVisuals)
                    Draw.Rectangle(this, "fvgS" + CurrentBar, false, 2, Low[2], 0, High[0],
                                   Brushes.Transparent, Brushes.Red, 12);
            }

            // Invalidacion: el precio atraviesa el hueco por completo.
            if (!double.IsNaN(bullFvgBot) && Close[0] < bullFvgBot)
            {
                bullFvgTop = double.NaN; bullFvgBot = double.NaN; bullFvgBar = -1;
            }
            if (!double.IsNaN(bearFvgTop) && Close[0] > bearFvgTop)
            {
                bearFvgTop = double.NaN; bearFvgBot = double.NaN; bearFvgBar = -1;
            }
        }

        // ===============================================================
        //  RIESGO Y EJECUCION
        // ===============================================================
        private void ActualizarControlDiario()
        {
            double realizado = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit - dayStartCum;
            double abierto   = Position.MarketPosition == MarketPosition.Flat
                             ? 0
                             : Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
            double pnlDia = realizado + abierto;

            bool porPerdida  = DailyLossLimit    > 0 && pnlDia <= -DailyLossLimit;
            bool porGanancia = DailyProfitTarget > 0 && pnlDia >=  DailyProfitTarget;
            bool porTrades   = MaxTradesPerDay   > 0 && tradesToday >= MaxTradesPerDay;

            dayBlocked = porPerdida || porGanancia || porTrades;

            // Si se rompio el limite duro, se cierra lo que este abierto.
            if ((porPerdida || porGanancia) && Position.MarketPosition != MarketPosition.Flat)
            {
                if (Position.MarketPosition == MarketPosition.Long) ExitLong();
                else                                               ExitShort();
            }
        }

        private void AbrirLargo(double atr, bool usdMode, double riskPts, double tgtPts,
                                double slStruct, int score)
        {
            posEntry = Close[0];
            posSL    = usdMode && !FitStructural ? posEntry - riskPts : slStruct;
            if (posSL >= posEntry - TickSize) return;   // stop invalido

            double R = posEntry - posSL;
            posTP1 = usdMode ? posEntry + tgtPts : posEntry + Tp1R * R;
            posTP2 = usdMode ? posEntry + tgtPts : posEntry + Tp2R * R;
            beDone = false;
            lastScore = score;

            int q1 = Contracts / 2;
            int q2 = Contracts - q1;

            // Con 1 contrato no hay parcial: se busca TP1 (igual que el
            // comportamiento validado en TradingView con qty=1).
            if (q1 > 0)
            {
                SetStopLoss("L1",   CalculationMode.Price, posSL,  false);
                SetProfitTarget("L1", CalculationMode.Price, posTP1);
                if (IdxTick > 0) EnterLong(IdxTick, q1, "L1"); else EnterLong(q1, "L1");
            }
            if (q2 > 0)
            {
                SetStopLoss("L2",   CalculationMode.Price, posSL, false);
                SetProfitTarget("L2", CalculationMode.Price, q1 > 0 ? posTP2 : posTP1);
                if (IdxTick > 0) EnterLong(IdxTick, q2, "L2"); else EnterLong(q2, "L2");
            }

            tradesToday++;
            cntIn++;
            lastEntryBar = CurrentBar;
            if (ShowVisuals)
            {
                Draw.ArrowUp(this, "eL" + CurrentBar, false, 0, Low[0] - 6 * TickSize, Brushes.Aqua);
                Draw.Text(this, "eLt" + CurrentBar, "L " + lastScore, 0, Low[0] - 12 * TickSize, Brushes.Aqua);
            }
        }

        private void AbrirCorto(double atr, bool usdMode, double riskPts, double tgtPts,
                                double slStruct, int score)
        {
            posEntry = Close[0];
            posSL    = usdMode && !FitStructural ? posEntry + riskPts : slStruct;
            if (posSL <= posEntry + TickSize) return;

            double R = posSL - posEntry;
            posTP1 = usdMode ? posEntry - tgtPts : posEntry - Tp1R * R;
            posTP2 = usdMode ? posEntry - tgtPts : posEntry - Tp2R * R;
            beDone = false;
            lastScore = score;

            int q1 = Contracts / 2;
            int q2 = Contracts - q1;

            if (q1 > 0)
            {
                SetStopLoss("S1",   CalculationMode.Price, posSL,  false);
                SetProfitTarget("S1", CalculationMode.Price, posTP1);
                if (IdxTick > 0) EnterShort(IdxTick, q1, "S1"); else EnterShort(q1, "S1");
            }
            if (q2 > 0)
            {
                SetStopLoss("S2",   CalculationMode.Price, posSL, false);
                SetProfitTarget("S2", CalculationMode.Price, q1 > 0 ? posTP2 : posTP1);
                if (IdxTick > 0) EnterShort(IdxTick, q2, "S2"); else EnterShort(q2, "S2");
            }

            tradesToday++;
            cntIn++;
            lastEntryBar = CurrentBar;
            if (ShowVisuals)
            {
                Draw.ArrowDown(this, "eS" + CurrentBar, false, 0, High[0] + 6 * TickSize, Brushes.Magenta);
                Draw.Text(this, "eSt" + CurrentBar, "S " + lastScore, 0, High[0] + 12 * TickSize, Brushes.Magenta);
            }
        }

        /// <summary>
        /// Breakeven evaluado tick a tick (no al cierre de vela) para que el
        /// disparo sea exacto. Solo modifica el stop una vez.
        /// </summary>
        private void GestionIntrabar()
        {
            GestionBarra(Closes[IdxTick][0]);
        }

        private void GestionBarra(double px)
        {
            if (!UseBreakeven || beDone) return;
            if (Position.MarketPosition == MarketPosition.Flat) return;
            if (posEntry <= 0 || posSL <= 0) return;

            // Se usa el precio MEDIO REAL de la posicion, no el cierre de la
            // vela de la senal: la entrada es a mercado y se llena en el tick
            // siguiente, asi que "breakeven" solo es breakeven de verdad si se
            // calcula sobre lo que realmente se pago.
            double ent = Position.AveragePrice > 0 ? Position.AveragePrice : posEntry;

            if (Position.MarketPosition == MarketPosition.Long)
            {
                double trig = ent + BeAtR * (ent - posSL);
                if (px >= trig && ent > posSL)
                {
                    beDone = true;
                    SetStopLoss("L1", CalculationMode.Price, ent, false);
                    SetStopLoss("L2", CalculationMode.Price, ent, false);
                }
            }
            else
            {
                double trig = ent - BeAtR * (posSL - ent);
                if (px <= trig && ent < posSL)
                {
                    beDone = true;
                    SetStopLoss("S1", CalculationMode.Price, ent, false);
                    SetStopLoss("S2", CalculationMode.Price, ent, false);
                }
            }
        }

        // ===============================================================
        //  UTILIDADES
        // ===============================================================
        private bool EnVentana(DateTime t, int startHHmm, int endHHmm)
        {
            int hm = t.Hour * 100 + t.Minute;
            if (startHHmm <= endHHmm) return hm >= startHHmm && hm < endHHmm;
            return hm >= startHHmm || hm < endHHmm;   // ventana que cruza medianoche
        }

        private void PintarPanel(bool sesionOK, bool htfBull, bool vwBull, double barRatio,
                                 int scoreBull, int scoreBear, bool winBull, bool winBear)
        {
            double realizado = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit - dayStartCum;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SMC ORDER FLOW MASTER");
            sb.AppendLine("Sesion    : " + (sesionOK ? "ACTIVA" : "FUERA"));
            sb.AppendLine("Sesgo HTF : " + (htfBull ? "Alcista" : "Bajista"));
            sb.AppendLine("VWAP      : " + (vwBull ? "Encima" : "Debajo"));
            int vPts = swBullBar >= 0 ? swBullVolPts + swBullMssVolPts
                     : swBearBar >= 0 ? swBearVolPts + swBearMssVolPts : 0;
            sb.AppendLine("Volumen   : " + (UseVolume ? "+" + vPts + " pt" : "Off"));
            sb.AppendLine("Flujo     : " + (DeltaSrc.ToString()));
            sb.AppendLine("Delta vela: " + (barBuy - barSell).ToString("N0")
                          + "  (" + (barRatio * 100).ToString("N0") + "%)");
            sb.AppendLine("Compra/Vta: " + barBuy.ToString("N0") + " / " + barSell.ToString("N0"));
            sb.AppendLine("CVD sesion: " + cvd.ToString("N0"));
            string setup = "Esperando barrido";
            if (winBull) setup = "LONG  " + scoreBull + " | " + (swBullMss ? "MSS OK" : "MSS...")
                                 + (swBullAbsorp ? " | ABS" : "");
            if (winBear) setup = "SHORT " + scoreBear + " | " + (swBearMss ? "MSS OK" : "MSS...")
                                 + (swBearAbsorp ? " | ABS" : "");
            sb.AppendLine("Setup     : " + setup);
            sb.AppendLine("Embudo    : " + cntSweep + " barridos > " + cntMss
                          + " MSS > " + cntIn + " entradas");
            sb.AppendLine("Trades hoy: " + tradesToday + (dayBlocked ? "  [BLOQUEADO]" : ""));
            sb.AppendLine("PnL hoy   : " + realizado.ToString("C0"));

            Draw.TextFixed(this, "smcDash", sb.ToString(), TextPosition.TopRight,
                           Brushes.White, new SimpleFont("Consolas", 12),
                           Brushes.DimGray, Brushes.Black, 60);
        }

        // ===============================================================
        //  PARAMETROS
        // ===============================================================

        #region 0) Rendimiento
        [NinjaScriptProperty]
        [Display(Name = "Cargar serie de 1 tick (order flow)", Order = 1, GroupName = "0) Rendimiento",
                 Description = "ON = delta real y llenado intrabar exacto, pero un ano de ticks de ES "
                             + "es lentisimo (usalo en tramos de 1-3 meses). OFF = modo rapido, sin order "
                             + "flow, sirve para correr el ano entero y sacar la linea base.")]
        public bool UsarSerieTick { get; set; }
        #endregion

        #region 1) Sesion
        [NinjaScriptProperty]
        [Display(Name = "Filtrar por sesion", Order = 1, GroupName = "1) Sesion (hora CT)")]
        public bool UseSession { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "Inicio (HHMM)", Order = 2, GroupName = "1) Sesion (hora CT)")]
        public int SesStart { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "Fin (HHMM)", Order = 3, GroupName = "1) Sesion (hora CT)")]
        public int SesEnd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Bloquear lunch", Order = 4, GroupName = "1) Sesion (hora CT)")]
        public bool UseLunchBlock { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "Lunch inicio (HHMM)", Order = 5, GroupName = "1) Sesion (hora CT)")]
        public int LunchStart { get; set; }

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "Lunch fin (HHMM)", Order = 6, GroupName = "1) Sesion (hora CT)")]
        public int LunchEnd { get; set; }

        [NinjaScriptProperty]
        [Range(-12, 12)]
        [Display(Name = "Ajuste horario a CT (horas)", Order = 7, GroupName = "1) Sesion (hora CT)",
                 Description = "0 si tu NT8 esta en hora Central. -1 si esta en hora de Nueva York.")]
        public int CtOffsetHours { get; set; }
        #endregion

        #region 2) Sesgo
        [NinjaScriptProperty]
        [Display(Name = "Usar sesgo del TF mayor", Order = 1, GroupName = "2) Sesgo")]
        public bool UseHtf { get; set; }

        [NinjaScriptProperty]
        [Range(1, 1440)]
        [Display(Name = "Minutos del TF mayor", Order = 2, GroupName = "2) Sesgo")]
        public int HtfMinutes { get; set; }

        [NinjaScriptProperty]
        [Range(2, 400)]
        [Display(Name = "EMA del TF mayor", Order = 3, GroupName = "2) Sesgo")]
        public int HtfEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Filtrar con VWAP de sesion", Order = 4, GroupName = "2) Sesgo")]
        public bool UseVwapFilter { get; set; }
        #endregion

        #region 3) Barrido
        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Longitud de pivote", Order = 1, GroupName = "3) Barrido de liquidez")]
        public int PivotLen { get; set; }

        [NinjaScriptProperty]
        [Range(2, 100)]
        [Display(Name = "Periodo ATR", Order = 2, GroupName = "3) Barrido de liquidez")]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 3.0)]
        [Display(Name = "Tolerancia EQH/EQL (x ATR)", Order = 3, GroupName = "3) Barrido de liquidez")]
        public double EqTolAtr { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 5.0)]
        [Display(Name = "Mecha minima de rechazo (x ATR)", Order = 4, GroupName = "3) Barrido de liquidez")]
        public double WickAtr { get; set; }
        #endregion

        #region 4) SMT
        [NinjaScriptProperty]
        [Display(Name = "Usar SMT", Order = 1, GroupName = "4) SMT")]
        public bool UseSmt { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Instrumento correlacionado", Order = 2, GroupName = "4) SMT",
                 Description = "Ej: NQ ##-##  (##-## = contrato frontal automatico)")]
        public string SmtInstrument { get; set; }
        #endregion

        #region 5) Volumen
        [NinjaScriptProperty]
        [Display(Name = "Usar volumen", Order = 1, GroupName = "5) Volumen (suma puntos, no filtra)")]
        public bool UseVolume { get; set; }

        [NinjaScriptProperty]
        [Range(2, 200)]
        [Display(Name = "Media de volumen", Order = 2, GroupName = "5) Volumen (suma puntos, no filtra)")]
        public int VolLen { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 10.0)]
        [Display(Name = "Volumen ALTO del barrido (x media) = +1", Order = 3, GroupName = "5) Volumen (suma puntos, no filtra)")]
        public double VolMult { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 20.0)]
        [Display(Name = "Volumen MUY ALTO del barrido (x media) = +2", Order = 4, GroupName = "5) Volumen (suma puntos, no filtra)")]
        public double VolMult2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Sumar el volumen de la vela del MSS", Order = 5, GroupName = "5) Volumen (suma puntos, no filtra)")]
        public bool UseMssVolume { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 20.0)]
        [Display(Name = "Volumen alto del MSS (x media) = +1", Order = 6, GroupName = "5) Volumen (suma puntos, no filtra)")]
        public double VolMssMult { get; set; }
        #endregion

        #region 5C) Order Flow
        [NinjaScriptProperty]
        [Display(Name = "Fuente del delta", Order = 1, GroupName = "5C) Order Flow",
                 Description = "ReglaDelTick: funciona con cualquier feed. BidAsk: mas exacto, "
                             + "necesita historico de bid/ask. Volumetric: necesita Lifetime u Order Flow+.")]
        public SmcDeltaSource DeltaSrc { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir delta del sweep a favor", Order = 2, GroupName = "5C) Order Flow",
                 Description = "Sobre ticks reales de ES: PF 1.14 -> 2.48 con umbral 0.0 (n=25 en 41 dias). Filtro independiente de la absorcion.")]
        public bool UseOrderFlow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Order flow como puntos de score", Order = 3, GroupName = "5C) Order Flow",
                 Description = "Si esta activo el flujo suma hasta 3 puntos (score max 10) en vez de ser requisito.")]
        public bool OrderFlowAsScore { get; set; }

        [NinjaScriptProperty]
        [Range(-1.0, 1.0)]
        [Display(Name = "Ratio minimo de delta en el barrido", Order = 4, GroupName = "5C) Order Flow",
                 Description = "(compra-venta)/(compra+venta) de la vela del barrido. 0.10 = 10% neto a favor.")]
        public double OfMinRatio { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir absorcion (divergencia CVD)", Order = 5, GroupName = "5C) Order Flow",
                 Description = "El precio hace un extremo nuevo pero el delta acumulado NO lo acompana. "
                             + "El filtro mas robusto sobre ticks reales de ES: PF 1.14 -> 1.77 (n=36), "
                             + "positivo en las dos mitades y en los dos lados.")]
        public bool UseAbsorption { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir delta en la vela del MSS", Order = 6, GroupName = "5C) Order Flow",
                 Description = "OJO: sobre ticks reales de ES este filtro RESTA (PF 0.96 con 0.10, 0.47 con 0.15). "
                             + "Entras persiguiendo el movimiento. Dejalo apagado salvo que tu propia data diga otra cosa.")]
        public bool UseMssDelta { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "Ratio minimo de delta en el MSS", Order = 7, GroupName = "5C) Order Flow")]
        public double OfMssMinRatio { get; set; }
        #endregion

        #region 6) FVG
        [NinjaScriptProperty]
        [Display(Name = "Exigir retorno al FVG", Order = 1, GroupName = "6) FVG")]
        public bool RequireFvg { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 3.0)]
        [Display(Name = "Tamano minimo del FVG (x ATR)", Order = 2, GroupName = "6) FVG")]
        public double FvgMinAtr { get; set; }
        #endregion

        #region 7) MSS
        [NinjaScriptProperty]
        [Display(Name = "Exigir MSS tras el barrido", Order = 1, GroupName = "7) MSS")]
        public bool RequireMss { get; set; }

        [NinjaScriptProperty]
        [Range(2, 20)]
        [Display(Name = "Pivote de estructura menor", Order = 2, GroupName = "7) MSS")]
        public int MssPivotLen { get; set; }
        #endregion

        #region 8) Senal y riesgo
        [NinjaScriptProperty]
        [Range(2, 100)]
        [Display(Name = "Ventana tras el barrido (velas)", Order = 1, GroupName = "8) Senal y riesgo")]
        public int WindowBars { get; set; }

        [NinjaScriptProperty]
        [Range(2, 13)]
        [Display(Name = "Score minimo (9 base, 12 con order flow)", Order = 2, GroupName = "8) Senal y riesgo",
                 Description = "El volumen aporta hasta 3 puntos (2 del barrido + 1 del MSS). Medido sobre el "
                             + "ano completo en 5m, subir el umbral NO mejora el PF (queda plano en 1.24-1.25) "
                             + "y solo recorta operaciones: deja 5 salvo que midas otra cosa.")]
        public int MinScore { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 5.0)]
        [Display(Name = "Colchon del SL (x ATR)", Order = 3, GroupName = "8) Senal y riesgo")]
        public double SlBufAtr { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 20.0)]
        [Display(Name = "TP1 (multiplo de R)", Order = 4, GroupName = "8) Senal y riesgo")]
        public double Tp1R { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 20.0)]
        [Display(Name = "TP2 (multiplo de R)", Order = 5, GroupName = "8) Senal y riesgo")]
        public double Tp2R { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo de riesgo", Order = 6, GroupName = "8) Senal y riesgo")]
        public SmcRiskMode RiskMode { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Contratos", Order = 7, GroupName = "8) Senal y riesgo",
                 Description = "Con 2 o mas se toma parcial en TP1 y el resto corre a TP2. Con 1 se busca TP1.")]
        public int Contracts { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100000)]
        [Display(Name = "Perdida maxima (USD)", Order = 8, GroupName = "8) Senal y riesgo")]
        public double MaxLossUsd { get; set; }

        [NinjaScriptProperty]
        [Range(1, 1000000)]
        [Display(Name = "Objetivo (USD)", Order = 9, GroupName = "8) Senal y riesgo")]
        public double TargetUsd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Solo entrar si el SL estructural cabe", Order = 10, GroupName = "8) Senal y riesgo")]
        public bool FitStructural { get; set; }
        #endregion

        #region 8D) Limite diario
        [NinjaScriptProperty]
        [Range(0, 1000000)]
        [Display(Name = "Stop de perdida diaria (USD, 0=off)", Order = 1, GroupName = "8D) Limite diario")]
        public double DailyLossLimit { get; set; }

        [NinjaScriptProperty]
        [Range(0, 1000000)]
        [Display(Name = "Objetivo diario (USD, 0=off)", Order = 2, GroupName = "8D) Limite diario",
                 Description = "Util para la regla de consistencia de las prop firms.")]
        public double DailyProfitTarget { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Maximo de trades por dia (0=off)", Order = 3, GroupName = "8D) Limite diario")]
        public int MaxTradesPerDay { get; set; }
        #endregion

        #region 9) Ejecucion
        [NinjaScriptProperty]
        [Display(Name = "Mover SL a breakeven", Order = 1, GroupName = "9) Ejecucion")]
        public bool UseBreakeven { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 20.0)]
        [Display(Name = "Breakeven a los (multiplo de R)", Order = 2, GroupName = "9) Ejecucion")]
        public double BeAtR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cerrar al terminar la sesion", Order = 3, GroupName = "9) Ejecucion")]
        public bool CloseAtSessionEnd { get; set; }
        #endregion

        #region 10) Visual
        [NinjaScriptProperty]
        [Display(Name = "Dibujar senales en el grafico", Order = 1, GroupName = "10) Visual")]
        public bool ShowVisuals { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar panel", Order = 2, GroupName = "10) Visual")]
        public bool ShowDashboard { get; set; }
        #endregion
    }
}
