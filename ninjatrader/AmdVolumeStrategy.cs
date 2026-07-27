// ===================================================================
//  AMD VOLUME - NinjaTrader 8 (NinjaScript / C#)
//  Acumulacion -> Manipulacion -> Distribucion, con volumen real.
//
//  LA IDEA (Power of Three / ICT):
//    ACUMULACION  el precio construye un rango en una sesion definida
//                 (premarket, overnight o Londres)
//    MANIPULACION en una ventana horaria concreta el precio SALE de ese
//                 rango, barre la liquidez que hay detras y CIERRA de
//                 vuelta dentro. Es el "judas swing"
//    DISTRIBUCION se entra al lado CONTRARIO del barrido
//
//  QUE LO DIFERENCIA DEL SMC ANTERIOR:
//  El sistema anterior barria pivotes LOCALES sin componente horario y
//  sobre el ano completo de ES dio PF 1.00 (301 trades, verificado en
//  NinjaTrader). Este barre el rango de una SESION en una ventana de
//  tiempo concreta. Sobre el mismo ano de datos fue lo MAS ROBUSTO a la
//  eleccion de parametros de todo lo probado: moviendo los bordes del
//  rango de acumulacion (12 variantes) el PF quedo entre 1.17 y 1.44,
//  todas positivas. Para comparar, la reversion a VWAP oscilaba entre
//  0.69 y 1.27 haciendo lo mismo.
//
//  AVISO HONESTO: en esa misma investigacion NO paso la prueba temporal.
//  Walk-forward por mitades 0.75 / 1.88, y quitando los dos mejores meses
//  el resultado del ano pasaba de +15.855 a -4.105. Un solo mes cargaba
//  el 71%. Esto es una HIPOTESIS a validar, no un sistema validado.
//  Lo que aqui se anade y no se pudo probar en Python es el VOLUMEN REAL
//  comprador/vendedor sobre todo el historico. Esa es la pregunta abierta.
//
//  ANTI-REPINTADO:
//  - Calculate = OnBarClose. Las senales solo se evaluan a vela cerrada.
//  - El rango de acumulacion se congela cuando termina su ventana; nunca
//    se recalcula hacia atras.
//  - El delta se acumula desde la serie de 1 tick y se cierra en el
//    cierre exacto de la vela primaria.
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
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>De donde sale el objetivo.</summary>
    public enum AmdTargetMode
    {
        MultiploDeR,      // objetivo = R x multiplo
        LadoOpuesto       // objetivo = el otro extremo del rango de acumulacion
    }

    /// <summary>Que se le exige al flujo en la vela de manipulacion.</summary>
    public enum AmdFlowMode
    {
        Ninguno,          // no se filtra por delta
        EnContra,         // delta agresivo CONTRA la entrada = absorcion (lo que sugiere la data)
        AFavor            // delta agresivo A FAVOR de la entrada (la lectura clasica)
    }

    public class AmdVolumeStrategy : Strategy
    {
        // ---------- series ----------
        private int IdxTick = -1;

        // ---------- order flow ----------
        private double accBuy, accSell;     // acumulador de la vela en curso
        private double barBuy, barSell;     // delta cerrado de la ultima vela
        private double lastTickPx = 0;
        private bool   lastTickBuy = true;

        // ---------- rango de acumulacion ----------
        private double rangoHi = double.NaN, rangoLo = double.NaN;
        private bool   rangoListo = false;
        private int    barrasRango = 0;
        private DateTime diaActual = DateTime.MinValue;

        // ---------- estado del dia ----------
        private int    opsHoy = 0;
        private bool   manipUsada = false;
        private double dayStartCum = 0;
        private bool   dayBlocked = false;

        // ---------- posicion ----------
        private double posEntry, posSL, posTP;
        private bool   beDone;
        private int    lastEntryBar = -1;

        // ---------- indicadores ----------
        private ATR atrInd;
        private SMA volAvg;

        // ---------- embudo ----------
        private int cntDias, cntManip, cntIn;
        private int cntNoFlujo, cntNoVol, cntNoRango, cntNoRiesgo, cntNoDia;
        private bool embudoImpreso = false;

        // ===============================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "AMD / Power of Three: rango de acumulacion, judas swing en ventana "
                            + "horaria y entrada al lado contrario, con volumen comprador/vendedor real.";
                Name        = "AmdVolumeStrategy";

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
                TraceOrders                               = false;
                RealtimeErrorHandling                     = RealtimeErrorHandling.StopCancelClose;
                BarsRequiredToTrade                       = 20;
                IsInstantiatedOnEachOptimizationIteration = true;

                // 0) Rendimiento
                UsarSerieTick   = false;   // OFF por defecto: permite correr el ano entero

                // 1) Ventanas (hora del ESTE, ET - asi se define ICT)
                RangoIni        = 400;     // 04:00 ET, premarket
                RangoFin        = 930;     // 09:30 ET
                ManipIni        = 930;     // 09:30 ET
                ManipFin        = 1030;    // 10:30 ET
                CierreET        = 1600;    // 16:00 ET
                OffsetHorasET   = 0;

                // 2) Calidad del rango
                MinBarrasRango  = 10;
                MinRangoAtr     = 0.0;
                MaxRangoAtr     = 0.0;     // 0 = sin limite

                // 3) Riesgo
                ModoObjetivo    = AmdTargetMode.MultiploDeR;
                MultiploR       = 2.0;
                SlBufTicks      = 1;
                Contratos       = 1;
                MinRPuntos      = 1.0;
                MaxRPuntos      = 40.0;

                // 4) Volumen y order flow
                ModoFlujo       = AmdFlowMode.Ninguno;
                RatioFlujoMin   = 0.10;
                UsarVolumen     = false;
                VolLen          = 20;
                VolMult         = 1.3;

                // 5) Limites
                MaxOpsDia       = 1;
                StopDiarioUsd   = 0;
                ObjetivoDiaUsd  = 0;
                UsarBreakeven   = false;
                BeAtR           = 1.0;

                // 6) Visual
                MostrarVisuales = true;
            }
            else if (State == State.Configure)
            {
                if (UsarSerieTick)
                {
                    IdxTick = 1;
                    AddDataSeries(BarsPeriodType.Tick, 1);
                }
                else IdxTick = -1;
            }
            else if (State == State.DataLoaded)
            {
                atrInd = ATR(14);
                volAvg = SMA(Volume, VolLen);
                Reset();
            }
            else if (State == State.Terminated)
            {
                ImprimirEmbudo();
            }
        }

        private void Reset()
        {
            accBuy = accSell = barBuy = barSell = 0;
            lastTickPx = 0; lastTickBuy = true;
            rangoHi = rangoLo = double.NaN; rangoListo = false; barrasRango = 0;
            diaActual = DateTime.MinValue;
            opsHoy = 0; manipUsada = false; dayStartCum = 0; dayBlocked = false;
            posEntry = posSL = posTP = 0; beDone = false; lastEntryBar = -1;
            cntDias = cntManip = cntIn = 0;
            cntNoFlujo = cntNoVol = cntNoRango = cntNoRiesgo = cntNoDia = 0;
            embudoImpreso = false;
        }

        // ===============================================================
        protected override void OnBarUpdate()
        {
            // --- serie de 1 tick: motor de delta ---
            if (IdxTick > 0 && BarsInProgress == IdxTick)
            {
                AcumularDelta();
                if (UsarBreakeven) GestionBe(Closes[IdxTick][0]);
                return;
            }
            if (BarsInProgress != 0) return;

            // Cerrar el delta de la vela que acaba de terminar.
            barBuy = accBuy; barSell = accSell; accBuy = accSell = 0;

            if (CurrentBar < BarsRequiredToTrade) return;
            double atr = atrInd[0];
            if (atr <= 0) return;

            DateTime et = Time[0].AddHours(OffsetHorasET);
            int hm = et.Hour * 100 + et.Minute;
            DateTime dia = DiaDeTrading(et, hm);

            // ---------- nuevo dia de trading ----------
            if (dia != diaActual)
            {
                diaActual = dia;
                rangoHi = rangoLo = double.NaN;
                rangoListo = false; barrasRango = 0;
                opsHoy = 0; manipUsada = false; dayBlocked = false;
                dayStartCum = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
                cntDias++;
            }

            // ---------- 1) ACUMULACION: construir el rango ----------
            if (EnVentana(hm, RangoIni, RangoFin))
            {
                rangoHi = double.IsNaN(rangoHi) ? High[0] : Math.Max(rangoHi, High[0]);
                rangoLo = double.IsNaN(rangoLo) ? Low[0]  : Math.Min(rangoLo, Low[0]);
                barrasRango++;
                rangoListo = false;
            }
            else if (barrasRango >= MinBarrasRango && !double.IsNaN(rangoHi) && !rangoListo)
            {
                // La ventana termino: el rango queda CONGELADO. No se recalcula.
                rangoListo = true;
                if (MostrarVisuales)
                {
                    Draw.Line(this, "rH" + CurrentBar, false, barrasRango, rangoHi, 0, rangoHi,
                              Brushes.DodgerBlue, DashStyleHelper.Dash, 1);
                    Draw.Line(this, "rL" + CurrentBar, false, barrasRango, rangoLo, 0, rangoLo,
                              Brushes.DodgerBlue, DashStyleHelper.Dash, 1);
                }
            }

            ActualizarControlDiario();

            // ---------- cierre por fin de sesion ----------
            if (Position.MarketPosition != MarketPosition.Flat && hm >= CierreET)
            {
                if (Position.MarketPosition == MarketPosition.Long) ExitLong();
                else                                                ExitShort();
            }

            // Sin serie de tick el breakeven se evalua a cierre de vela.
            if (IdxTick < 0 && UsarBreakeven) GestionBe(Close[0]);

            if (CurrentBar >= Bars.Count - 2) ImprimirEmbudo();

            // ---------- 2) MANIPULACION ----------
            if (!rangoListo || manipUsada) return;
            if (!EnVentana(hm, ManipIni, ManipFin)) return;
            if (Position.MarketPosition != MarketPosition.Flat) return;
            if (CurrentBar <= lastEntryBar) return;

            double rango = rangoHi - rangoLo;
            if (rango <= 0) return;

            // calidad del rango: ni ridiculamente estrecho ni desbocado
            if (MinRangoAtr > 0 && rango < MinRangoAtr * atr) { cntNoRango++; manipUsada = true; return; }
            if (MaxRangoAtr > 0 && rango > MaxRangoAtr * atr) { cntNoRango++; manipUsada = true; return; }

            int dir = 0;
            double ext = 0;
            // barre el MAXIMO y cierra dentro -> la distribucion va abajo -> CORTO
            if (High[0] > rangoHi && Close[0] < rangoHi) { dir = -1; ext = High[0]; }
            // barre el MINIMO y cierra dentro -> la distribucion va arriba -> LARGO
            else if (Low[0] < rangoLo && Close[0] > rangoLo) { dir = 1; ext = Low[0]; }
            if (dir == 0) return;

            cntManip++;
            manipUsada = true;   // el judas swing del dia solo cuenta una vez

            // ---------- filtros de volumen y flujo ----------
            if (UsarVolumen && Volume[0] < volAvg[0] * VolMult) { cntNoVol++; return; }

            if (ModoFlujo != AmdFlowMode.Ninguno && HayFlujo())
            {
                double tot = barBuy + barSell;
                double ratio = tot > 0 ? (barBuy - barSell) / tot : 0.0;
                // ratio a favor de la DIRECCION DE ENTRADA
                double aFavor = dir > 0 ? ratio : -ratio;
                bool ok = ModoFlujo == AmdFlowMode.AFavor
                          ? aFavor >=  RatioFlujoMin
                          : aFavor <= -RatioFlujoMin;   // EnContra = absorcion
                if (!ok) { cntNoFlujo++; return; }
            }

            if (dayBlocked || (MaxOpsDia > 0 && opsHoy >= MaxOpsDia)) { cntNoDia++; return; }

            // ---------- 3) DISTRIBUCION: entrada ----------
            double e  = Close[0];
            double sl = dir > 0 ? ext - SlBufTicks * TickSize : ext + SlBufTicks * TickSize;
            double R  = Math.Abs(e - sl);
            if (R < MinRPuntos || R > MaxRPuntos) { cntNoRiesgo++; return; }

            double tp;
            if (ModoObjetivo == AmdTargetMode.LadoOpuesto)
                tp = dir > 0 ? rangoHi : rangoLo;
            else
                tp = dir > 0 ? e + MultiploR * R : e - MultiploR * R;

            // objetivo del lado equivocado -> no hay trade
            if ((dir > 0 && tp <= e) || (dir < 0 && tp >= e)) { cntNoRiesgo++; return; }

            posEntry = e; posSL = sl; posTP = tp; beDone = false;

            if (dir > 0)
            {
                SetStopLoss("A", CalculationMode.Price, sl, false);
                SetProfitTarget("A", CalculationMode.Price, tp);
                if (IdxTick > 0) EnterLong(IdxTick, Contratos, "A"); else EnterLong(Contratos, "A");
                if (MostrarVisuales)
                    Draw.ArrowUp(this, "eL" + CurrentBar, false, 0, Low[0] - 4 * TickSize, Brushes.Aqua);
            }
            else
            {
                SetStopLoss("A", CalculationMode.Price, sl, false);
                SetProfitTarget("A", CalculationMode.Price, tp);
                if (IdxTick > 0) EnterShort(IdxTick, Contratos, "A"); else EnterShort(Contratos, "A");
                if (MostrarVisuales)
                    Draw.ArrowDown(this, "eS" + CurrentBar, false, 0, High[0] + 4 * TickSize, Brushes.Magenta);
            }
            opsHoy++; cntIn++; lastEntryBar = CurrentBar;
        }

        // ===============================================================
        //  AUXILIARES
        // ===============================================================

        /// <summary>Dia de trading de futuros: a partir de las 18:00 ET cuenta el dia siguiente.</summary>
        private DateTime DiaDeTrading(DateTime et, int hm)
        {
            return hm >= 1800 ? et.Date.AddDays(1) : et.Date;
        }

        /// <summary>Ventana horaria que puede cruzar medianoche.</summary>
        private bool EnVentana(int hm, int ini, int fin)
        {
            if (ini <= fin) return hm >= ini && hm < fin;
            return hm >= ini || hm < fin;
        }

        private bool HayFlujo()
        {
            return IdxTick > 0;
        }

        private void AcumularDelta()
        {
            double px = Closes[IdxTick][0];
            double v  = Volumes[IdxTick][0];
            if (v <= 0) { lastTickPx = px; return; }
            // Regla del tick: uptick = compra, downtick = venta, igual = hereda.
            bool buy = px > lastTickPx ? true : (px < lastTickPx ? false : lastTickBuy);
            if (buy) accBuy += v; else accSell += v;
            lastTickPx = px; lastTickBuy = buy;
        }

        private void GestionBe(double px)
        {
            if (beDone || Position.MarketPosition == MarketPosition.Flat) return;
            if (posEntry <= 0 || posSL <= 0) return;
            double ent = Position.AveragePrice > 0 ? Position.AveragePrice : posEntry;
            if (Position.MarketPosition == MarketPosition.Long)
            {
                if (ent > posSL && px >= ent + BeAtR * (ent - posSL))
                { beDone = true; SetStopLoss("A", CalculationMode.Price, ent, false); }
            }
            else
            {
                if (ent < posSL && px <= ent - BeAtR * (posSL - ent))
                { beDone = true; SetStopLoss("A", CalculationMode.Price, ent, false); }
            }
        }

        private void ActualizarControlDiario()
        {
            double real = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit - dayStartCum;
            double abierto = Position.MarketPosition == MarketPosition.Flat
                           ? 0 : Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency, Close[0]);
            double pnl = real + abierto;
            bool porPerdida  = StopDiarioUsd  > 0 && pnl <= -StopDiarioUsd;
            bool porGanancia = ObjetivoDiaUsd > 0 && pnl >=  ObjetivoDiaUsd;
            dayBlocked = porPerdida || porGanancia;
            if (dayBlocked && Position.MarketPosition != MarketPosition.Flat)
            {
                if (Position.MarketPosition == MarketPosition.Long) ExitLong();
                else                                                ExitShort();
            }
        }

        private void ImprimirEmbudo()
        {
            if (embudoImpreso || cntDias == 0) return;
            embudoImpreso = true;
            Print("===== AMD VOLUME - EMBUDO =====");
            Print("  Instrumento / TF       : " + Instrument.MasterInstrument.Name + " "
                  + BarsPeriod.Value + " " + BarsPeriod.BarsPeriodType);
            Print("  Serie de 1 tick        : " + (UsarSerieTick ? "SI" : "NO (modo rapido)"));
            Print("  Acumulacion            : " + RangoIni + "-" + RangoFin + " ET");
            Print("  Manipulacion           : " + ManipIni + "-" + ManipFin + " ET");
            Print("  Modo de flujo          : " + ModoFlujo);
            Print("  Dias procesados        : " + cntDias);
            Print("  Judas swing detectados : " + cntManip);
            Print("  Descartados por rango  : " + cntNoRango);
            Print("  Descartados por volumen: " + cntNoVol);
            Print("  Descartados por flujo  : " + cntNoFlujo);
            Print("  Descartados por riesgo : " + cntNoRiesgo);
            Print("  Descartados por lim.dia: " + cntNoDia);
            Print("  ENTRADAS               : " + cntIn);
            Print("===============================");
        }

        // ===============================================================
        //  PARAMETROS
        // ===============================================================
        #region 0) Rendimiento
        [NinjaScriptProperty]
        [Display(Name = "Cargar serie de 1 tick (order flow)", Order = 1, GroupName = "0) Rendimiento",
                 Description = "ON = delta real, pero un ano de ticks de ES es lentisimo (usalo en "
                             + "tramos de 1-3 meses). OFF = modo rapido para correr el ano entero.")]
        public bool UsarSerieTick { get; set; }
        #endregion

        #region 1) Ventanas (hora ET)
        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Acumulacion inicio (HHMM ET)", Order = 1, GroupName = "1) Ventanas (hora ET)",
                 Description = "400 = premarket. 1800 = overnight completo. 200 = Londres.")]
        public int RangoIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Acumulacion fin (HHMM ET)", Order = 2, GroupName = "1) Ventanas (hora ET)")]
        public int RangoFin { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Manipulacion inicio (HHMM ET)", Order = 3, GroupName = "1) Ventanas (hora ET)")]
        public int ManipIni { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Manipulacion fin (HHMM ET)", Order = 4, GroupName = "1) Ventanas (hora ET)")]
        public int ManipFin { get; set; }

        [NinjaScriptProperty] [Range(0, 2359)]
        [Display(Name = "Cierre forzado (HHMM ET)", Order = 5, GroupName = "1) Ventanas (hora ET)")]
        public int CierreET { get; set; }

        [NinjaScriptProperty] [Range(-12, 12)]
        [Display(Name = "Ajuste horario a ET (horas)", Order = 6, GroupName = "1) Ventanas (hora ET)",
                 Description = "0 si tu NinjaTrader esta en hora del Este. +1 si esta en hora Central.")]
        public int OffsetHorasET { get; set; }
        #endregion

        #region 2) Calidad del rango
        [NinjaScriptProperty] [Range(1, 1000)]
        [Display(Name = "Velas minimas en el rango", Order = 1, GroupName = "2) Calidad del rango")]
        public int MinBarrasRango { get; set; }

        [NinjaScriptProperty] [Range(0.0, 20.0)]
        [Display(Name = "Rango minimo (x ATR, 0=off)", Order = 2, GroupName = "2) Calidad del rango")]
        public double MinRangoAtr { get; set; }

        [NinjaScriptProperty] [Range(0.0, 50.0)]
        [Display(Name = "Rango maximo (x ATR, 0=off)", Order = 3, GroupName = "2) Calidad del rango")]
        public double MaxRangoAtr { get; set; }
        #endregion

        #region 3) Riesgo
        [NinjaScriptProperty]
        [Display(Name = "Modo de objetivo", Order = 1, GroupName = "3) Riesgo",
                 Description = "MultiploDeR fue mejor que LadoOpuesto en la investigacion (1.63 vs 1.19 a 2R).")]
        public AmdTargetMode ModoObjetivo { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Multiplo de R", Order = 2, GroupName = "3) Riesgo")]
        public double MultiploR { get; set; }

        [NinjaScriptProperty] [Range(0, 100)]
        [Display(Name = "Colchon del SL (ticks)", Order = 3, GroupName = "3) Riesgo")]
        public int SlBufTicks { get; set; }

        [NinjaScriptProperty] [Range(1, 100)]
        [Display(Name = "Contratos", Order = 4, GroupName = "3) Riesgo")]
        public int Contratos { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1000.0)]
        [Display(Name = "R minimo (puntos)", Order = 5, GroupName = "3) Riesgo")]
        public double MinRPuntos { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1000.0)]
        [Display(Name = "R maximo (puntos)", Order = 6, GroupName = "3) Riesgo")]
        public double MaxRPuntos { get; set; }
        #endregion

        #region 4) Volumen y order flow
        [NinjaScriptProperty]
        [Display(Name = "Flujo en la vela de manipulacion", Order = 1, GroupName = "4) Volumen y order flow",
                 Description = "EnContra = el delta agresivo va CONTRA la entrada (absorcion). Es lo que "
                             + "sugieren los ticks reales de ES: los barridos con flujo a favor rendian "
                             + "-0.863 ATR y los que tenian flujo en contra +0.168 ATR. Requiere la serie de 1 tick.")]
        public AmdFlowMode ModoFlujo { get; set; }

        [NinjaScriptProperty] [Range(0.0, 1.0)]
        [Display(Name = "Ratio minimo de delta", Order = 2, GroupName = "4) Volumen y order flow")]
        public double RatioFlujoMin { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Exigir volumen alto", Order = 3, GroupName = "4) Volumen y order flow")]
        public bool UsarVolumen { get; set; }

        [NinjaScriptProperty] [Range(2, 200)]
        [Display(Name = "Media de volumen", Order = 4, GroupName = "4) Volumen y order flow")]
        public int VolLen { get; set; }

        [NinjaScriptProperty] [Range(0.5, 10.0)]
        [Display(Name = "Volumen minimo (x media)", Order = 5, GroupName = "4) Volumen y order flow")]
        public double VolMult { get; set; }
        #endregion

        #region 5) Limites
        [NinjaScriptProperty] [Range(0, 20)]
        [Display(Name = "Maximo de operaciones por dia", Order = 1, GroupName = "5) Limites")]
        public int MaxOpsDia { get; set; }

        [NinjaScriptProperty] [Range(0, 1000000)]
        [Display(Name = "Stop de perdida diaria (USD, 0=off)", Order = 2, GroupName = "5) Limites")]
        public double StopDiarioUsd { get; set; }

        [NinjaScriptProperty] [Range(0, 1000000)]
        [Display(Name = "Objetivo diario (USD, 0=off)", Order = 3, GroupName = "5) Limites")]
        public double ObjetivoDiaUsd { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mover SL a breakeven", Order = 4, GroupName = "5) Limites")]
        public bool UsarBreakeven { get; set; }

        [NinjaScriptProperty] [Range(0.1, 20.0)]
        [Display(Name = "Breakeven a los (multiplo de R)", Order = 5, GroupName = "5) Limites")]
        public double BeAtR { get; set; }
        #endregion

        #region 6) Visual
        [NinjaScriptProperty]
        [Display(Name = "Dibujar en el grafico", Order = 1, GroupName = "6) Visual")]
        public bool MostrarVisuales { get; set; }
        #endregion
    }
}
