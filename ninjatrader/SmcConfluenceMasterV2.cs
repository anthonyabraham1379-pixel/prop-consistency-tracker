// ===================================================================
//  SMC CONFLUENCE MASTER v2 - NinjaTrader 8 (NinjaScript / C#)
//  Port FIEL del Pine Script "SMC Confluence Master v2 Backtest".
//  Misma logica, mismos parametros, mismos valores por defecto.
//  Encima, una capa de ORDER FLOW que SOLO SUMA PUNTOS y nunca veta.
//
//  MODELO EN 3 PASOS (identico al Pine)
//    PASO 1  Barrido de liquidez (swing/EQH/EQL con mecha de rechazo)
//    PASO 2  MSS/CHoCH: el precio rompe la estructura menor en contra
//            del barrido, confirmando que fue sweep y no continuacion
//    PASO 3  Entrada al confirmar el MSS (opcional: exigir ademas el
//            retorno al FVG, modo estricto)
//
//  SCORE DE CONFLUENCIA (max 7, igual que el Pine)
//    2 fijos por el barrido + HTF + VWAP + Volumen + SMT + bonus EQH/EQL.
//    Un filtro apagado NO penaliza.
//
//  ORDER FLOW (esto es lo unico que el Pine no tenia)
//    Delta de la vela, CVD de la sesion, absorcion y stacked imbalances.
//    Por defecto SUMAN puntos al score y jamas bloquean una entrada: si
//    no tienes barras Volumetric o el flujo contradice, la operacion
//    sigue siendo valida y solo puntua menos. OfModo permite exigirlo
//    para medir la diferencia, pero viene en "Puntuar".
//
//  DIFERENCIA REAL FRENTE AL PROBADOR DE TRADINGVIEW (medida, no teorica)
//  En Pine, strategy.exit con qty_percent=50 sobre UN contrato no puede
//  partir la posicion: TradingView redondea y cierra todo en TP1. Es
//  decir, lo que ves en el Probador con 1 contrato NO es "1.5R + 2.5R",
//  es objetivo unico en 1.5R. Aqui se replica ese comportamiento: con
//  Contratos = 1 hay un solo objetivo en TP1; con 2 o mas se parte 50/50
//  entre TP1 y TP2, que es lo que el Pine pretendia hacer.
//
//  Lo que si se comprobo que NO explica la diferencia TV/NT: el momento
//  de llenado (cierre de la vela de senal frente a apertura de la
//  siguiente) y la resolucion intrabar. Medidos sobre un ano de ES dan
//  0.95 frente a 0.95 y 0.92 frente a 0.92.
//
//  ANTI-REPINTADO
//  - Senales solo al cierre de vela (Calculate.OnBarClose).
//  - Sesgo HTF con la vela del TF mayor CERRADA (indice [1]).
//  - Pivotes confirmados con retardo fijo, nunca se redibujan.
//
//  MODO DE RIESGO
//  - "Estructura": SL en el extremo del barrido + colchon ATR, TP por R.
//  - "USD fijo": perdida y objetivo en dolares. Con AjustarAlRiesgo
//    activado solo entra si el SL estructural CABE en la perdida maxima.
//    Recomendado: medido, el peor dia del sistema (-3.763 USD) lo produjo
//    UNA sola operacion, no una racha, asi que el limite diario no lo
//    evita y este filtro si.
//
//  APLICAR SOBRE: 15m o 1H (tambien funciona en 1m-5m).
//  VOLUMETRIC (Lifetime u Order Flow+) es OPCIONAL.
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
    /// <summary>Modo de calculo del stop y del objetivo.</summary>
    public enum ScmRiesgo { Estructura = 0, UsdFijo = 1 }

    /// <summary>Que papel juega el order flow en la decision.</summary>
    public enum ScmOfModo
    {
        Ignorar = 0,   // no se lee el footprint
        Puntuar = 1,   // SUMA puntos al score, nunca bloquea  <-- por defecto
        Exigir  = 2    // ademas exige confirmacion (solo para medir)
    }

    public class SmcConfluenceMasterV2 : Strategy
    {
        // ---------- indices de series ----------
        private int IdxHtf = 1;
        private int IdxSmt = 2;
        private int IdxVol = 3;

        // ---------- swings mayores (barrido) ----------
        private double lastPH = double.NaN, prevPH = double.NaN;
        private double lastPL = double.NaN, prevPL = double.NaN;
        private bool   eqhTag = false, eqlTag = false;

        // ---------- swings menores (MSS) ----------
        private double minorPH = double.NaN, minorPL = double.NaN;

        // ---------- swings del simbolo correlacionado (SMT) ----------
        private double corrPH = double.NaN, corrPL = double.NaN;

        // ---------- FVG ----------
        private double bullFvgTop = double.NaN, bullFvgBot = double.NaN;
        private double bearFvgTop = double.NaN, bearFvgBot = double.NaN;
        private int    bullFvgBar = -1, bearFvgBar = -1;

        // ---------- maquina de setup ----------
        private int    swBullBar = -1, swBearBar = -1;
        private double swBullExt = 0, swBearExt = 0;
        private double swBullMssLvl = double.NaN, swBearMssLvl = double.NaN;
        private bool   swBullVol = false, swBearVol = false;
        private bool   swBullSMT = false, swBearSMT = false;
        private bool   swBullEQ = false,  swBearEQ = false;
        private bool   swBullMss = false, swBearMss = false;
        private bool   swBullDisp = false, swBearDisp = false;

        // ---------- VWAP de sesion ----------
        private double vwapPV = 0, vwapVol = 0, vwapVal = 0;

        // ---------- order flow ----------
        private bool   ofValido = false;
        private double ofDelta = 0, ofDeltaPct = 0, ofVolTotal = 0;
        private double ofMaxSeen = 0, ofMinSeen = 0;
        private int    ofImbCompra = 0, ofImbVenta = 0;
        private double cvdSesion = 0;
        private int    cvdBarraSesion = 0;
        private readonly List<double> cvdHist = new List<double>();
        private double absPrecioRef = double.NaN, absAcumDelta = 0;
        private int    absBarras = 0;

        // ---------- gestion de la operacion ----------
        private string senalActual = "";
        private double posEntrada = 0, posSL = 0, posTP1 = 0, posTP2 = 0;
        private bool   beHecho = false, parcialHecho = false;
        private int    dirActual = 0;

        // ---------- diagnostico (el embudo del panel del Pine) ----------
        private int cntSweep = 0, cntMss = 0, cntListo = 0, cntSes = 0, cntIn = 0;
        private int cntOfConfirma = 0, cntOfNiega = 0;
        private readonly List<int> scoresVistos = new List<int>();
        private bool embudoVolcado = false;

        // ===============================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Port fiel del SMC Confluence Master v2 de Pine, con una capa "
                            + "de order flow que suma puntos y nunca veta una entrada.";
                Name        = "SmcConfluenceMasterV2";
                Calculate   = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 60;
                BarsRequiredToTrade = 60;
                IncludeCommission = true;
                IsInstantiatedOnEachOptimizationIteration = true;

                // --- 1) Filtro horario (hora del grafico, ver OffsetHoras) ---
                UsarSesion   = true;
                SesionIni    = 730;    // 07:30 CT
                SesionFin    = 1400;   // 14:00 CT
                EvitarLunch  = true;
                LunchIni     = 1130;
                LunchFin     = 1300;
                OffsetHoras  = 0;

                // --- 2) Sesgo (TF mayor + VWAP) ---
                UsarHtf      = true;
                MinutosHtf   = 60;
                EmaHtf       = 50;
                UsarVwap     = false;

                // --- 3) Barrido de liquidez ---
                PivoteSwing  = 4;
                TolEqAtr     = 0.25;
                MechaAtr     = 0.50;
                Permitir2Velas = false;

                // --- 4) SMT ---
                UsarSmt      = true;
                SimboloSmt   = "NQ ##-##";   // ##-## = vencimiento frontal automatico

                // --- 5) Volumen ---
                UsarVolumen  = false;
                MediaVolumen = 20;
                MultVolumen  = 1.3;

                // --- 6) FVG ---
                FvgMinAtr    = 0.15;

                // --- 7) Confirmacion MSS ---
                ExigirMss    = true;
                PivoteMenor  = 2;

                // --- 8) Senal y riesgo ---
                VentanaVelas = 7;
                ExigirFvg    = false;
                ScoreMinimo  = 5;
                ColchonSlAtr = 0.50;
                Tp1R         = 1.5;
                Tp2R         = 2.5;

                // --- 8b) Modo de riesgo ---
                ModoRiesgo   = ScmRiesgo.Estructura;
                Contratos    = 1;
                RiesgoUsd    = 400;
                ObjetivoUsd  = 1000;
                AjustarAlRiesgo = true;

                // --- 8c) Mejoras experimentales (apagadas, igual que el Pine) ---
                UsarPremiumDiscount = false;
                VelasPd      = 120;
                UsarDesplazamiento = false;
                CuerpoMinAtr = 0.6;

                // --- 9) ORDER FLOW (el plus) ---
                OfModo          = ScmOfModo.Puntuar;
                PtsOfDelta      = 1;
                PtsOfAbsorcion  = 1;
                PtsOfImbalance  = 1;
                OfDeltaMinPct   = 0.15;
                OfRatioImbalance = 3.0;
                OfMinImbalances = 2;
                OfAbsorcionVelas = 3;
                OfAbsorcionRangoTicks = 24;
                OfAbsorcionDeltaMin = 0.25;
                OfUsarCvd       = true;

                // --- 10) Gestion ---
                UsarBreakeven = true;
                CerrarEnSesion = true;

                // --- 11) Diagnostico ---
                MostrarVisuales = true;
                ImprimirEmbudo  = true;
            }
            else if (State == State.Configure)
            {
                IdxHtf = 1;
                AddDataSeries(BarsPeriodType.Minute, MinutosHtf);

                IdxSmt = 2;
                if (UsarSmt && !string.IsNullOrEmpty(SimboloSmt))
                    AddDataSeries(SimboloSmt, BarsPeriod.BarsPeriodType, BarsPeriod.Value);
                else
                    AddDataSeries(BarsPeriod.BarsPeriodType, BarsPeriod.Value);

                IdxVol = 3;
                AddVolumetric(null, BarsPeriod.BarsPeriodType, BarsPeriod.Value,
                              VolumetricDeltaType.BidAsk, 1);
            }
            else if (State == State.DataLoaded)
            {
                ResetEstado();
            }
            else if (State == State.Terminated)
            {
                if (ImprimirEmbudo && !embudoVolcado) { VolcarEmbudo(); embudoVolcado = true; }
            }
        }

        private void ResetEstado()
        {
            lastPH = prevPH = lastPL = prevPL = double.NaN;
            minorPH = minorPL = corrPH = corrPL = double.NaN;
            bullFvgTop = bullFvgBot = bearFvgTop = bearFvgBot = double.NaN;
            bullFvgBar = bearFvgBar = -1;
            swBullBar = swBearBar = -1;
            swBullMss = swBearMss = false;
            eqhTag = eqlTag = false;
            vwapPV = vwapVol = vwapVal = 0;
            ofValido = false; ofDelta = ofDeltaPct = ofVolTotal = 0;
            ofImbCompra = ofImbVenta = 0;
            cvdSesion = 0; cvdHist.Clear(); cvdBarraSesion = 0;
            absPrecioRef = double.NaN; absAcumDelta = 0; absBarras = 0;
            senalActual = ""; posEntrada = posSL = posTP1 = posTP2 = 0;
            beHecho = parcialHecho = false; dirActual = 0;
            cntSweep = cntMss = cntListo = cntSes = cntIn = 0;
            cntOfConfirma = cntOfNiega = 0;
            scoresVistos.Clear(); embudoVolcado = false;
        }

        // ===============================================================
        //  BUCLE PRINCIPAL
        // ===============================================================
        protected override void OnBarUpdate()
        {
            if (BarsInProgress == IdxVol) { LeerOrderFlow(); return; }
            if (BarsInProgress == IdxSmt) { ActualizarSmt(); return; }
            if (BarsInProgress == IdxHtf) { return; }
            if (BarsInProgress != 0) return;

            int necesarias = Math.Max(PivoteSwing, PivoteMenor) * 2 + EmaHtf + 5;
            if (CurrentBar < necesarias) return;
            if (CurrentBars[IdxHtf] < 2) return;

            // ---------- corte de sesion ----------
            if (Bars.IsFirstBarOfSession)
            {
                vwapPV = 0; vwapVol = 0;
                cvdSesion = 0; cvdHist.Clear(); cvdBarraSesion = CurrentBar;
            }

            // ---------- VWAP de sesion ----------
            double hlc3 = (High[0] + Low[0] + Close[0]) / 3.0;
            vwapPV  += hlc3 * Volume[0];
            vwapVol += Volume[0];
            vwapVal  = vwapVol > 0 ? vwapPV / vwapVol : Close[0];

            // ---------- CVD ----------
            if (ofValido)
            {
                cvdSesion += ofDelta;
                cvdHist.Add(cvdSesion);
                if (cvdHist.Count > 500) cvdHist.RemoveAt(0);
            }
            ActualizarAbsorcion();

            double atr = ATR(14)[0];
            if (atr <= 0) return;

            // ---------- pivotes confirmados (no repintan) ----------
            ActualizarPivotes(atr);

            // ---------- FVG ----------
            ActualizarFvg(atr);

            // ---------- barrido de liquidez ----------
            DetectarBarrido(atr);

            // ---------- MSS ----------
            DetectarMss(atr);

            // ---------- expirar setups ----------
            if (swBullBar >= 0 && (CurrentBar - swBullBar) > VentanaVelas) { swBullBar = -1; swBullMss = false; }
            if (swBearBar >= 0 && (CurrentBar - swBearBar) > VentanaVelas) { swBearBar = -1; swBearMss = false; }

            // ---------- gestion de la posicion abierta ----------
            if (Position.MarketPosition != MarketPosition.Flat) { GestionarPosicion(); return; }
            if (senalActual.Length > 0) { senalActual = ""; dirActual = 0; beHecho = parcialHecho = false; }

            EvaluarEntrada(atr);
        }

        // ===============================================================
        //  PIVOTES (equivalente a ta.pivothigh / ta.pivotlow)
        //  Un pivote de longitud n se confirma n barras DESPUES, con lo
        //  cual nunca se redibuja. Es el mismo retardo que en Pine.
        // ===============================================================
        private bool EsPivoteAlto(int n, int desplazamiento)
        {
            double p = High[n + desplazamiento];
            for (int i = 0; i <= n * 2; i++)
            {
                if (i == n) continue;
                if (High[i + desplazamiento] >= p) return false;
            }
            return true;
        }

        private bool EsPivoteBajo(int n, int desplazamiento)
        {
            double p = Low[n + desplazamiento];
            for (int i = 0; i <= n * 2; i++)
            {
                if (i == n) continue;
                if (Low[i + desplazamiento] <= p) return false;
            }
            return true;
        }

        private void ActualizarPivotes(double atr)
        {
            int n = PivoteSwing;
            if (CurrentBar > n * 2 + 1)
            {
                if (EsPivoteAlto(n, 0))
                {
                    prevPH = lastPH; lastPH = High[n];
                    eqhTag = !double.IsNaN(prevPH) && Math.Abs(lastPH - prevPH) <= TolEqAtr * atr;
                    if (MostrarVisuales)
                        Draw.Line(this, "ph" + CurrentBar, false, n, lastPH, 0, lastPH,
                                  Brushes.IndianRed, DashStyleHelper.Dot, 1);
                }
                if (EsPivoteBajo(n, 0))
                {
                    prevPL = lastPL; lastPL = Low[n];
                    eqlTag = !double.IsNaN(prevPL) && Math.Abs(lastPL - prevPL) <= TolEqAtr * atr;
                    if (MostrarVisuales)
                        Draw.Line(this, "pl" + CurrentBar, false, n, lastPL, 0, lastPL,
                                  Brushes.SeaGreen, DashStyleHelper.Dot, 1);
                }
            }

            int m = PivoteMenor;
            if (CurrentBar > m * 2 + 1)
            {
                if (EsPivoteAlto(m, 0)) minorPH = High[m];
                if (EsPivoteBajo(m, 0)) minorPL = Low[m];
            }
        }

        /// <summary>Pivotes del simbolo correlacionado, para el SMT.</summary>
        private void ActualizarSmt()
        {
            if (!UsarSmt) return;
            int n = PivoteSwing;
            if (CurrentBars[IdxSmt] < n * 2 + 2) return;
            bool alto = true, bajo = true;
            double ph = Highs[IdxSmt][n], pl = Lows[IdxSmt][n];
            for (int i = 0; i <= n * 2; i++)
            {
                if (i == n) continue;
                if (Highs[IdxSmt][i] >= ph) alto = false;
                if (Lows[IdxSmt][i]  <= pl) bajo = false;
            }
            if (alto) corrPH = ph;
            if (bajo) corrPL = pl;
        }

        // ===============================================================
        //  FVG
        // ===============================================================
        private void ActualizarFvg(double atr)
        {
            if (CurrentBar < 3) return;

            if (Low[0] > High[2] && (Low[0] - High[2]) >= FvgMinAtr * atr)
            {
                bullFvgTop = Low[0]; bullFvgBot = High[2]; bullFvgBar = CurrentBar;
                if (MostrarVisuales)
                    Draw.Rectangle(this, "fvgU" + CurrentBar, false, 2, Low[0], 0, High[2],
                                   Brushes.Transparent, Brushes.SeaGreen, 15);
            }
            if (High[0] < Low[2] && (Low[2] - High[0]) >= FvgMinAtr * atr)
            {
                bearFvgTop = Low[2]; bearFvgBot = High[0]; bearFvgBar = CurrentBar;
                if (MostrarVisuales)
                    Draw.Rectangle(this, "fvgD" + CurrentBar, false, 2, Low[2], 0, High[0],
                                   Brushes.Transparent, Brushes.IndianRed, 15);
            }

            // invalidacion: el precio atraviesa el hueco por completo
            if (!double.IsNaN(bullFvgBot) && Close[0] < bullFvgBot)
            { bullFvgTop = bullFvgBot = double.NaN; bullFvgBar = -1; }
            if (!double.IsNaN(bearFvgTop) && Close[0] > bearFvgTop)
            { bearFvgTop = bearFvgBot = double.NaN; bearFvgBar = -1; }
        }

        private bool EnFvgAlcista()
        {
            return !double.IsNaN(bullFvgBot) && Low[0] <= bullFvgTop && Close[0] >= bullFvgBot;
        }

        private bool EnFvgBajista()
        {
            return !double.IsNaN(bearFvgTop) && High[0] >= bearFvgBot && Close[0] <= bearFvgTop;
        }

        // ===============================================================
        //  BARRIDO DE LIQUIDEZ
        // ===============================================================
        private void DetectarBarrido(double atr)
        {
            double mechaSup = High[0] - Math.Max(Open[0], Close[0]);
            double mechaInf = Math.Min(Open[0], Close[0]) - Low[0];
            bool mechaBajOK = mechaSup >= MechaAtr * atr;
            bool mechaAlcOK = mechaInf >= MechaAtr * atr;

            bool volAhora = false, volAntes = false;
            if (UsarVolumen)
            {
                double media = SMA(Volume, MediaVolumen)[0];
                volAhora = media > 0 && Volume[0] >= media * MultVolumen;
                if (CurrentBar > MediaVolumen + 1)
                {
                    double mediaP = SMA(Volume, MediaVolumen)[1];
                    volAntes = mediaP > 0 && Volume[1] >= mediaP * MultVolumen;
                }
            }

            // sweep de 1 vela: toma el nivel con mecha y CIERRA de vuelta dentro
            bool swBear1 = !double.IsNaN(lastPH) && High[0] > lastPH && Close[0] < lastPH && mechaBajOK;
            bool swBull1 = !double.IsNaN(lastPL) && Low[0]  < lastPL && Close[0] > lastPL && mechaAlcOK;

            // sweep de 2 velas: la anterior rompe y cierra fuera, esta cierra dentro
            bool swBear2 = Permitir2Velas && !double.IsNaN(lastPH) && High[1] > lastPH
                           && Close[1] >= lastPH && Close[0] < lastPH;
            bool swBull2 = Permitir2Velas && !double.IsNaN(lastPL) && Low[1] < lastPL
                           && Close[1] <= lastPL && Close[0] > lastPL;

            bool sweepBear = swBear1 || swBear2;
            bool sweepBull = swBull1 || swBull2;

            if (sweepBear)
            {
                swBearBar = CurrentBar;
                swBearExt = swBear2 ? Math.Max(High[0], High[1]) : High[0];
                swBearVol = volAhora || (swBear2 && volAntes);
                swBearSMT = UsarSmt && !double.IsNaN(corrPH) && CurrentBars[IdxSmt] > 0
                            && Highs[IdxSmt][0] < corrPH;
                swBearEQ  = eqhTag;
                swBearMss = false; swBearDisp = false;
                swBearMssLvl = double.IsNaN(minorPL) ? Low[0] : minorPL;
                cntSweep++;
                if (MostrarVisuales)
                    Draw.TriangleDown(this, "swD" + CurrentBar, false, 0,
                                      High[0] + 2 * TickSize, Brushes.OrangeRed);
            }
            if (sweepBull)
            {
                swBullBar = CurrentBar;
                swBullExt = swBull2 ? Math.Min(Low[0], Low[1]) : Low[0];
                swBullVol = volAhora || (swBull2 && volAntes);
                swBullSMT = UsarSmt && !double.IsNaN(corrPL) && CurrentBars[IdxSmt] > 0
                            && Lows[IdxSmt][0] > corrPL;
                swBullEQ  = eqlTag;
                swBullMss = false; swBullDisp = false;
                swBullMssLvl = double.IsNaN(minorPH) ? High[0] : minorPH;
                cntSweep++;
                if (MostrarVisuales)
                    Draw.TriangleUp(this, "swU" + CurrentBar, false, 0,
                                    Low[0] - 2 * TickSize, Brushes.Gold);
            }
        }

        // ===============================================================
        //  MSS / CHoCH
        // ===============================================================
        private void DetectarMss(double atr)
        {
            if (swBullBar >= 0 && !swBullMss && !double.IsNaN(swBullMssLvl) && Close[0] > swBullMssLvl)
            {
                swBullMss = true;
                swBullDisp = Math.Abs(Close[0] - Open[0]) >= CuerpoMinAtr * atr;
                cntMss++;
                if (MostrarVisuales)
                    Draw.Text(this, "mssU" + CurrentBar, "MSS", 0, High[0] + 4 * TickSize, Brushes.SeaGreen);
            }
            if (swBearBar >= 0 && !swBearMss && !double.IsNaN(swBearMssLvl) && Close[0] < swBearMssLvl)
            {
                swBearMss = true;
                swBearDisp = Math.Abs(Close[0] - Open[0]) >= CuerpoMinAtr * atr;
                cntMss++;
                if (MostrarVisuales)
                    Draw.Text(this, "mssD" + CurrentBar, "MSS", 0, Low[0] - 4 * TickSize, Brushes.IndianRed);
            }
        }

        // ===============================================================
        //  ORDER FLOW - lectura del footprint (una pasada por vela)
        // ===============================================================
        private void LeerOrderFlow()
        {
            ofValido = false;
            ofDelta = ofDeltaPct = ofVolTotal = ofMaxSeen = ofMinSeen = 0;
            ofImbCompra = ofImbVenta = 0;
            if (OfModo == ScmOfModo.Ignorar) return;

            try
            {
                VolumetricBarsType vb = BarsArray[IdxVol].BarsType as VolumetricBarsType;
                if (vb == null) return;
                var v = vb.Volumes[CurrentBars[IdxVol]];
                if (v == null) return;

                double lo = Lows[IdxVol][0], hi = Highs[IdxVol][0];
                if (hi <= lo) return;
                int pasos = (int)Math.Round((hi - lo) / TickSize) + 1;
                if (pasos < 2 || pasos > 4000) return;

                ofDelta    = v.BarDelta;
                ofVolTotal = Volumes[IdxVol][0];
                ofDeltaPct = ofVolTotal > 0 ? ofDelta / ofVolTotal : 0;
                ofMaxSeen  = v.MaxSeenDelta;
                ofMinSeen  = v.MinSeenDelta;

                // Imbalance en DIAGONAL: el comprador agresivo paga el ask y el
                // vendedor pega en el bid, asi que no compiten en el mismo nivel.
                int rachaC = 0, rachaV = 0;
                double bidAnterior = 0;
                for (int i = 0; i < pasos; i++)
                {
                    double p   = lo + i * TickSize;
                    double ask = v.GetAskVolumeForPrice(p);
                    double bid = v.GetBidVolumeForPrice(p);
                    if (i > 0)
                    {
                        if (ask > 0 && bidAnterior > 0 && ask >= bidAnterior * OfRatioImbalance)
                        { rachaC++; if (rachaC > ofImbCompra) ofImbCompra = rachaC; }
                        else rachaC = 0;
                        if (bidAnterior > 0 && ask > 0 && bidAnterior >= ask * OfRatioImbalance)
                        { rachaV++; if (rachaV > ofImbVenta) ofImbVenta = rachaV; }
                        else rachaV = 0;
                    }
                    bidAnterior = bid;
                }
                ofValido = true;
            }
            catch (Exception ex)
            {
                Print("Volumetric no disponible (Lifetime u Order Flow+): " + ex.Message
                      + "  -- el sistema sigue operando sin el plus de order flow.");
            }
        }

        private void ActualizarAbsorcion()
        {
            if (!ofValido) { absBarras = 0; absAcumDelta = 0; return; }
            double rangoMax = OfAbsorcionRangoTicks * TickSize;
            if (double.IsNaN(absPrecioRef)) absPrecioRef = Close[0];
            if (Math.Abs(Close[0] - absPrecioRef) <= rangoMax)
            { absBarras++; absAcumDelta += ofDelta; }
            else
            { absBarras = 0; absAcumDelta = 0; absPrecioRef = Close[0]; }
        }

        private bool HayAbsorcion(int dir)
        {
            if (!ofValido || absBarras < OfAbsorcionVelas || ofVolTotal <= 0) return false;
            double ratio = absAcumDelta / Math.Max(ofVolTotal * absBarras, 1);
            // Para un LARGO queremos delta VENDEDOR sin que el precio ceda.
            return dir > 0 ? ratio <= -OfAbsorcionDeltaMin : ratio >= OfAbsorcionDeltaMin;
        }

        private bool CvdApoya(int dir)
        {
            if (!OfUsarCvd || !ofValido) return false;
            int n = cvdHist.Count;
            if (n < 20) return false;
            int ventana = Math.Min(n - 1, Math.Min(30, CurrentBar - cvdBarraSesion));
            if (ventana < 10) return false;
            double dCvd = cvdSesion - cvdHist[n - 1 - ventana];
            return dir > 0 ? dCvd > 0 : dCvd < 0;
        }

        /// <summary>
        /// Puntos que el order flow SUMA al score. Nunca resta ni bloquea.
        /// </summary>
        private int PuntosOrderFlow(int dir, out string detalle)
        {
            detalle = "";
            if (OfModo == ScmOfModo.Ignorar || !ofValido) return 0;
            int pts = 0;

            bool deltaOK = dir * ofDeltaPct >= OfDeltaMinPct;
            if (!deltaOK && OfUsarCvd) deltaOK = CvdApoya(dir);
            if (deltaOK) { pts += PtsOfDelta; detalle += " delta"; }

            if (HayAbsorcion(dir)) { pts += PtsOfAbsorcion; detalle += " absorcion"; }

            int imb = dir > 0 ? ofImbCompra : ofImbVenta;
            if (imb >= OfMinImbalances) { pts += PtsOfImbalance; detalle += " imb" + imb; }

            if (pts > 0) cntOfConfirma++; else cntOfNiega++;
            return pts;
        }

        // ===============================================================
        //  SCORE Y ENTRADA
        // ===============================================================
        private bool EnVentanaHoraria(out int hhmm)
        {
            DateTime t = Time[0].AddHours(OffsetHoras);
            hhmm = t.Hour * 100 + t.Minute;
            if (UsarSesion && (hhmm < SesionIni || hhmm >= SesionFin)) return false;
            if (EvitarLunch && hhmm >= LunchIni && hhmm < LunchFin) return false;
            return true;
        }

        private void EvaluarEntrada(double atr)
        {
            int hhmm;
            bool sesionOK = EnVentanaHoraria(out hhmm);

            // sesgo del TF mayor con la vela CERRADA -> no repinta
            double emaHtf = EMA(Closes[IdxHtf], EmaHtf)[1];
            bool htfBull = Close[0] > emaHtf;
            bool htfBear = Close[0] < emaHtf;
            bool vwBull  = Close[0] > vwapVal;
            bool vwBear  = Close[0] < vwapVal;

            // Premium / Discount
            double pdHi = MAX(High, VelasPd)[0], pdLo = MIN(Low, VelasPd)[0];
            double pdEq = (pdHi + pdLo) / 2.0;
            bool pdLongOK  = !UsarPremiumDiscount || Close[0] < pdEq;
            bool pdShortOK = !UsarPremiumDiscount || Close[0] > pdEq;

            bool usdMode = ModoRiesgo == ScmRiesgo.UsdFijo;
            double pvQty = Instrument.MasterInstrument.PointValue * Contratos;
            double riskPts = pvQty > 0 ? RiesgoUsd / pvQty : 0;

            for (int k = 0; k < 2; k++)
            {
                int dir = k == 0 ? 1 : -1;
                int barra = dir > 0 ? swBullBar : swBearBar;
                if (barra < 0 || (CurrentBar - barra) > VentanaVelas || CurrentBar == barra) continue;

                bool mssOK = !ExigirMss || (dir > 0 ? swBullMss : swBearMss);
                if (!mssOK) continue;

                bool fvgOK;
                if (dir > 0) fvgOK = EnFvgAlcista() && bullFvgBar >= barra;
                else         fvgOK = EnFvgBajista() && bearFvgBar >= barra;
                if (ExigirFvg && !fvgOK) continue;

                bool dispOK = !UsarDesplazamiento || (dir > 0 ? swBullDisp : swBearDisp);
                if (!dispOK) continue;
                if (dir > 0 && !pdLongOK)  continue;
                if (dir < 0 && !pdShortOK) continue;

                // stop estructural
                double ext = dir > 0 ? swBullExt : swBearExt;
                double slEstructural = dir > 0 ? ext - ColchonSlAtr * atr : ext + ColchonSlAtr * atr;

                // En modo USD con ajuste, solo entra si el stop LOGICO cabe en el
                // riesgo. Es lo que corta las operaciones que se desmadran: medido,
                // el peor dia del sistema lo produjo UNA sola operacion.
                bool riesgoOK = true;
                if (usdMode && AjustarAlRiesgo)
                    riesgoOK = (dir > 0 ? Close[0] - slEstructural : slEstructural - Close[0]) <= riskPts;
                if (!riesgoOK) continue;

                cntListo++;
                if (!sesionOK) continue;
                cntSes++;

                // ---------- SCORE (identico al Pine) ----------
                bool htfOK = dir > 0 ? htfBull : htfBear;
                bool vwOK  = dir > 0 ? vwBull  : vwBear;
                bool volOK = dir > 0 ? swBullVol : swBearVol;
                bool smtOK = dir > 0 ? swBullSMT : swBearSMT;
                bool eqOK  = dir > 0 ? swBullEQ  : swBearEQ;

                int score = 2;
                score += (!UsarHtf     || htfOK) ? 1 : 0;
                score += (!UsarVwap    || vwOK)  ? 1 : 0;
                score += (!UsarVolumen || volOK) ? 1 : 0;
                score += (!UsarSmt     || smtOK) ? 1 : 0;
                score += eqOK ? 1 : 0;

                // ---------- ORDER FLOW: SOLO SUMA ----------
                string ofDet;
                int ptsOf = PuntosOrderFlow(dir, out ofDet);
                int scoreTotal = score + ptsOf;

                // Modo "Exigir" existe unicamente para medir la diferencia.
                // Por defecto NO esta activo y el flujo jamas bloquea nada.
                if (OfModo == ScmOfModo.Exigir && ptsOf <= 0) continue;

                scoresVistos.Add(scoreTotal);
                if (scoreTotal < ScoreMinimo) continue;

                Disparar(dir, scoreTotal, score, ptsOf, ofDet, slEstructural, riskPts, usdMode, atr);
                return;
            }
        }

        private void Disparar(int dir, int scoreTotal, int scoreBase, int ptsOf, string ofDet,
                              double slEstructural, double riskPts, bool usdMode, double atr)
        {
            posEntrada = Close[0];
            posSL = (usdMode && !AjustarAlRiesgo)
                  ? posEntrada - riskPts * dir
                  : slEstructural;

            double R = Math.Abs(posEntrada - posSL);
            if (R < TickSize * 2) return;

            if (usdMode)
            {
                double pvQty = Instrument.MasterInstrument.PointValue * Contratos;
                double tgtPts = pvQty > 0 ? ObjetivoUsd / pvQty : 0;
                posTP1 = posEntrada + tgtPts * dir;
                posTP2 = posTP1;
            }
            else
            {
                posTP1 = posEntrada + Tp1R * R * dir;
                posTP2 = posEntrada + Tp2R * R * dir;
            }

            dirActual = dir;
            beHecho = false; parcialHecho = false;
            senalActual = (dir > 0 ? "SMC_L" : "SMC_S") + scoreTotal;
            cntIn++;

            // El stop protege desde el primer tick: se coloca ANTES de entrar.
            SetStopLoss(senalActual, CalculationMode.Price, posSL, false);

            // Con 1 contrato TradingView no puede partir la posicion y cierra
            // todo en TP1. Con 2 o mas si se parte, que es lo que el Pine queria.
            bool parte = Contratos >= 2 && !usdMode;
            SetProfitTarget(senalActual, CalculationMode.Price, parte ? posTP2 : posTP1);

            if (dir > 0) EnterLong(Contratos, senalActual);
            else         EnterShort(Contratos, senalActual);

            if (MostrarVisuales)
            {
                Brush cc = dir > 0 ? Brushes.SeaGreen : Brushes.IndianRed;
                if (dir > 0) Draw.ArrowUp(this, "en" + CurrentBar, false, 0, Low[0] - 6 * TickSize, cc);
                else         Draw.ArrowDown(this, "en" + CurrentBar, false, 0, High[0] + 6 * TickSize, cc);
                Draw.Text(this, "sc" + CurrentBar, scoreTotal.ToString(), 0,
                          dir > 0 ? Low[0] - 12 * TickSize : High[0] + 12 * TickSize, cc);
            }

            if (ImprimirEmbudo)
                Print(Time[0] + "  " + (dir > 0 ? "COMPRA" : "VENTA")
                      + "  score " + scoreBase + "/7"
                      + (ptsOf > 0 ? " +" + ptsOf + " OF(" + ofDet.Trim() + ")" : " (sin OF)")
                      + " = " + scoreTotal
                      + " | E " + posEntrada.ToString("F2")
                      + " SL " + posSL.ToString("F2")
                      + " TP1 " + posTP1.ToString("F2")
                      + " TP2 " + posTP2.ToString("F2")
                      + " | R " + R.ToString("F2") + " pts");
        }

        // ===============================================================
        //  GESTION: parcial en TP1, breakeven y cierre de sesion
        // ===============================================================
        private void GestionarPosicion()
        {
            if (senalActual.Length == 0 || dirActual == 0) return;
            bool largo = dirActual > 0;
            double entrada = Position.AveragePrice;

            // parcial: solo tiene sentido con 2 o mas contratos
            if (!parcialHecho && Position.Quantity >= 2 && ModoRiesgo == ScmRiesgo.Estructura)
            {
                bool toco = largo ? High[0] >= posTP1 : Low[0] <= posTP1;
                if (toco)
                {
                    int mitad = Position.Quantity / 2;
                    if (mitad > 0)
                    {
                        if (largo) ExitLong(mitad, "TP1", senalActual);
                        else       ExitShort(mitad, "TP1", senalActual);
                    }
                    parcialHecho = true;
                }
            }

            // breakeven al tocar TP1 (igual que el Pine)
            if (UsarBreakeven && !beHecho)
            {
                bool toco = largo ? High[0] >= posTP1 : Low[0] <= posTP1;
                if (toco) { SetStopLoss(senalActual, CalculationMode.Price, entrada, false); beHecho = true; }
            }

            // cerrar al salir de la ventana horaria
            if (CerrarEnSesion)
            {
                int hhmm;
                if (!EnVentanaHoraria(out hhmm) && hhmm >= SesionFin)
                {
                    if (largo) ExitLong("FinSesion", senalActual);
                    else       ExitShort("FinSesion", senalActual);
                }
            }
        }

        // ===============================================================
        //  EMBUDO (el panel del Pine, en el Output)
        // ===============================================================
        private void VolcarEmbudo()
        {
            Print("");
            Print("=== EMBUDO SmcConfluenceMasterV2 ===");
            Print("  score minimo      : " + ScoreMinimo + "   order flow: " + OfModo);
            Print("  barridos          : " + cntSweep);
            Print("  MSS confirmados   : " + cntMss);
            Print("  setups listos     : " + cntListo);
            Print("  dentro de sesion  : " + cntSes);
            Print("  ENTRADAS          : " + cntIn);
            Print("  --- order flow (solo suma, nunca bloquea) ---");
            Print("    setups que confirmo: " + cntOfConfirma);
            Print("    setups que no      : " + cntOfNiega);
            if (cntOfConfirma == 0 && cntOfNiega > 0)
                Print("  *** El order flow no confirmo NADA: probablemente no tienes "
                      + "barras Volumetric. El sistema opero con el score del Pine. ***");
            if (scoresVistos.Count > 0)
            {
                Print("  --- cuantas entradas tendrias con cada score minimo ---");
                for (int c = 2; c <= 10; c++)
                {
                    int q = 0;
                    foreach (int s in scoresVistos) if (s >= c) q++;
                    if (q > 0) Print("    score >= " + c + " -> " + q + " entradas");
                }
            }
            Print("====================================");
        }

        // ===============================================================
        //  PARAMETROS
        // ===============================================================
        #region 1) Filtro horario
        [NinjaScriptProperty]
        [Display(Name = "Filtrar por sesion", Order = 1, GroupName = "1) Filtro horario")]
        public bool UsarSesion { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Inicio de sesion (HHMM)", Order = 2, GroupName = "1) Filtro horario")]
        public int SesionIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Fin de sesion (HHMM)", Order = 3, GroupName = "1) Filtro horario")]
        public int SesionFin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Bloquear el lunch", Order = 4, GroupName = "1) Filtro horario")]
        public bool EvitarLunch { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Inicio del lunch (HHMM)", Order = 5, GroupName = "1) Filtro horario")]
        public int LunchIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Fin del lunch (HHMM)", Order = 6, GroupName = "1) Filtro horario")]
        public int LunchFin { get; set; }

        [NinjaScriptProperty] [Range(-12, 12)]
        [Display(Name = "Offset horario", Order = 7, GroupName = "1) Filtro horario",
                 Description = "Las horas por defecto son de Chicago (CT), igual que el Pine. "
                             + "Si NinjaTrader te muestra hora de Nueva York, pon -1.")]
        public int OffsetHoras { get; set; }
        #endregion

        #region 2) Sesgo
        [NinjaScriptProperty]
        [Display(Name = "Usar sesgo del TF mayor", Order = 1, GroupName = "2) Sesgo")]
        public bool UsarHtf { get; set; }

        [NinjaScriptProperty] [Range(1, 1440)]
        [Display(Name = "Minutos del TF mayor", Order = 2, GroupName = "2) Sesgo",
                 Description = "El Pine usa 15m->1H y 1H->4H. En 15m pon 60; en 1H pon 240.")]
        public int MinutosHtf { get; set; }

        [NinjaScriptProperty] [Range(2, 400)]
        [Display(Name = "EMA del TF mayor", Order = 3, GroupName = "2) Sesgo")]
        public int EmaHtf { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Usar VWAP de sesion", Order = 4, GroupName = "2) Sesgo")]
        public bool UsarVwap { get; set; }
        #endregion

        #region 3) Barrido de liquidez
        [NinjaScriptProperty] [Range(2, 30)]
        [Display(Name = "Longitud del pivote", Order = 1, GroupName = "3) Barrido de liquidez")]
        public int PivoteSwing { get; set; }

        [NinjaScriptProperty] [Range(0.0, 3.0)]
        [Display(Name = "Tolerancia EQH/EQL (x ATR)", Order = 2, GroupName = "3) Barrido de liquidez")]
        public double TolEqAtr { get; set; }

        [NinjaScriptProperty] [Range(0.0, 5.0)]
        [Display(Name = "Mecha minima de rechazo (x ATR)", Order = 3, GroupName = "3) Barrido de liquidez")]
        public double MechaAtr { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Permitir barrido en 2 velas", Order = 4, GroupName = "3) Barrido de liquidez")]
        public bool Permitir2Velas { get; set; }
        #endregion

        #region 4) SMT
        [NinjaScriptProperty]
        [Display(Name = "Usar SMT", Order = 1, GroupName = "4) SMT")]
        public bool UsarSmt { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Simbolo correlacionado", Order = 2, GroupName = "4) SMT",
                 Description = "NQ ##-## resuelve solo al vencimiento frontal. Esa notacion "
                             + "funciona dentro del codigo de NinjaScript, no en el desplegable "
                             + "del Strategy Analyzer.")]
        public string SimboloSmt { get; set; }
        #endregion

        #region 5) Volumen
        [NinjaScriptProperty]
        [Display(Name = "Usar volumen en el score", Order = 1, GroupName = "5) Volumen")]
        public bool UsarVolumen { get; set; }

        [NinjaScriptProperty] [Range(2, 200)]
        [Display(Name = "Media de volumen", Order = 2, GroupName = "5) Volumen")]
        public int MediaVolumen { get; set; }

        [NinjaScriptProperty] [Range(0.1, 10.0)]
        [Display(Name = "Volumen minimo del barrido (x media)", Order = 3, GroupName = "5) Volumen")]
        public double MultVolumen { get; set; }
        #endregion

        #region 6) FVG
        [NinjaScriptProperty] [Range(0.0, 3.0)]
        [Display(Name = "Tamano minimo del FVG (x ATR)", Order = 1, GroupName = "6) FVG")]
        public double FvgMinAtr { get; set; }
        #endregion

        #region 7) Confirmacion MSS
        [NinjaScriptProperty]
        [Display(Name = "Exigir MSS tras el barrido", Order = 1, GroupName = "7) Confirmacion MSS")]
        public bool ExigirMss { get; set; }

        [NinjaScriptProperty] [Range(2, 20)]
        [Display(Name = "Pivote menor (estructura)", Order = 2, GroupName = "7) Confirmacion MSS")]
        public int PivoteMenor { get; set; }
        #endregion

        #region 8) Senal y riesgo
        [NinjaScriptProperty] [Range(3, 60)]
        [Display(Name = "Ventana tras el barrido (velas)", Order = 1, GroupName = "8) Senal y riesgo")]
        public int VentanaVelas { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir retorno al FVG (estricto)", Order = 2, GroupName = "8) Senal y riesgo")]
        public bool ExigirFvg { get; set; }

        [NinjaScriptProperty] [Range(2, 12)]
        [Display(Name = "Score minimo", Order = 3, GroupName = "8) Senal y riesgo",
                 Description = "El score del Pine llega a 7. Con el order flow sumando puede "
                             + "llegar a 10, asi que subirlo por encima de 7 equivale a exigir flujo.")]
        public int ScoreMinimo { get; set; }

        [NinjaScriptProperty] [Range(0.0, 5.0)]
        [Display(Name = "Colchon del SL (x ATR)", Order = 4, GroupName = "8) Senal y riesgo")]
        public double ColchonSlAtr { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "TP1 (multiplo de R)", Order = 5, GroupName = "8) Senal y riesgo")]
        public double Tp1R { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "TP2 (multiplo de R)", Order = 6, GroupName = "8) Senal y riesgo",
                 Description = "Solo actua con 2 o mas contratos. Con 1 contrato se cierra "
                             + "todo en TP1, que es lo que hace TradingView de verdad.")]
        public double Tp2R { get; set; }
        #endregion

        #region 8b) Modo de riesgo
        [NinjaScriptProperty]
        [Display(Name = "Modo de SL/TP", Order = 1, GroupName = "8b) Modo de riesgo")]
        public ScmRiesgo ModoRiesgo { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Contratos", Order = 2, GroupName = "8b) Modo de riesgo")]
        public int Contratos { get; set; }

        [NinjaScriptProperty] [Range(1, 100000)]
        [Display(Name = "Perdida maxima (USD)", Order = 3, GroupName = "8b) Modo de riesgo")]
        public double RiesgoUsd { get; set; }

        [NinjaScriptProperty] [Range(1, 100000)]
        [Display(Name = "Objetivo de ganancia (USD)", Order = 4, GroupName = "8b) Modo de riesgo")]
        public double ObjetivoUsd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Solo entrar si el SL cabe en el riesgo", Order = 5, GroupName = "8b) Modo de riesgo",
                 Description = "Descarta las entradas cuyo stop estructural no quepa en la "
                             + "perdida maxima, en vez de recortar el stop a un sitio ilogico. "
                             + "Medido: el peor dia del sistema lo causo UNA sola operacion, "
                             + "asi que este filtro protege mas que un limite diario.")]
        public bool AjustarAlRiesgo { get; set; }
        #endregion

        #region 8c) Experimentales
        [NinjaScriptProperty]
        [Display(Name = "Filtro Premium/Discount", Order = 1, GroupName = "8c) Experimentales")]
        public bool UsarPremiumDiscount { get; set; }

        [NinjaScriptProperty] [Range(20, 1000)]
        [Display(Name = "Velas del rango P/D", Order = 2, GroupName = "8c) Experimentales")]
        public int VelasPd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir desplazamiento en el MSS", Order = 3, GroupName = "8c) Experimentales")]
        public bool UsarDesplazamiento { get; set; }

        [NinjaScriptProperty] [Range(0.0, 5.0)]
        [Display(Name = "Cuerpo minimo del MSS (x ATR)", Order = 4, GroupName = "8c) Experimentales")]
        public double CuerpoMinAtr { get; set; }
        #endregion

        #region 9) Order flow (el plus)
        [NinjaScriptProperty]
        [Display(Name = "Papel del order flow", Order = 1, GroupName = "9) Order flow (el plus)",
                 Description = "Puntuar (por defecto) = SUMA puntos y nunca bloquea. "
                             + "Exigir = solo para medir cuanto aporta. Ignorar = Pine puro.")]
        public ScmOfModo OfModo { get; set; }

        [NinjaScriptProperty] [Range(0, 5)]
        [Display(Name = "Puntos por delta a favor", Order = 2, GroupName = "9) Order flow (el plus)")]
        public int PtsOfDelta { get; set; }

        [NinjaScriptProperty] [Range(0, 5)]
        [Display(Name = "Puntos por absorcion", Order = 3, GroupName = "9) Order flow (el plus)")]
        public int PtsOfAbsorcion { get; set; }

        [NinjaScriptProperty] [Range(0, 5)]
        [Display(Name = "Puntos por stacked imbalance", Order = 4, GroupName = "9) Order flow (el plus)")]
        public int PtsOfImbalance { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Delta minimo (porcentaje)", Order = 5, GroupName = "9) Order flow (el plus)")]
        public double OfDeltaMinPct { get; set; }

        [NinjaScriptProperty] [Range(1.1, 20.0)]
        [Display(Name = "Ratio de imbalance", Order = 6, GroupName = "9) Order flow (el plus)")]
        public double OfRatioImbalance { get; set; }

        [NinjaScriptProperty] [Range(1, 20)]
        [Display(Name = "Imbalances apiladas minimas", Order = 7, GroupName = "9) Order flow (el plus)")]
        public int OfMinImbalances { get; set; }

        [NinjaScriptProperty] [Range(1, 30)]
        [Display(Name = "Velas de absorcion", Order = 8, GroupName = "9) Order flow (el plus)")]
        public int OfAbsorcionVelas { get; set; }

        [NinjaScriptProperty] [Range(1, 400)]
        [Display(Name = "Rango de absorcion (ticks)", Order = 9, GroupName = "9) Order flow (el plus)",
                 Description = "Debe ser MAYOR que el rango tipico de una vela de tu marco, "
                             + "o la propia vela rompe la ventana y la absorcion no se activa nunca.")]
        public int OfAbsorcionRangoTicks { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Delta acumulado minimo", Order = 10, GroupName = "9) Order flow (el plus)")]
        public double OfAbsorcionDeltaMin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Usar el CVD de sesion", Order = 11, GroupName = "9) Order flow (el plus)")]
        public bool OfUsarCvd { get; set; }
        #endregion

        #region 10) Gestion
        [NinjaScriptProperty]
        [Display(Name = "Breakeven al tocar TP1", Order = 1, GroupName = "10) Gestion")]
        public bool UsarBreakeven { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cerrar al terminar la sesion", Order = 2, GroupName = "10) Gestion")]
        public bool CerrarEnSesion { get; set; }
        #endregion

        #region 11) Diagnostico
        [NinjaScriptProperty]
        [Display(Name = "Mostrar visuales", Order = 1, GroupName = "11) Diagnostico")]
        public bool MostrarVisuales { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Imprimir embudo en Output", Order = 2, GroupName = "11) Diagnostico")]
        public bool ImprimirEmbudo { get; set; }
        #endregion
    }
}
