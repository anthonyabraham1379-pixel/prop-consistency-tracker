/**
 * Lógica central de cálculo para el tracker de consistencia.
 * Mantener esta lógica aquí y no duplicarla en pantallas/componentes.
 *
 * Un "challenge" tiene esta forma:
 * {
 *   id, name, accountSize, profitTarget, maxDrawdown,
 *   drawdownType: 'static' | 'trailing',
 *   consistencyLimitPct: number (ej. 40) | null (sin regla),
 *   minProfitableDays: number,
 *   trades: [{ id, date (ISO), amount (number, +/-) }]
 * }
 *
 * Los trades son entradas individuales (puede haber varios el mismo día).
 * Las reglas de consistencia, drawdown y días rentables se calculan sobre
 * el TOTAL POR DÍA CALENDARIO — por eso casi todo aquí agrupa primero con
 * groupTradesByDay() antes de aplicar la regla.
 */

export function getTotalProfit(trades) {
  return trades.reduce((sum, t) => sum + t.amount, 0);
}

/**
 * Agrupa los trades por día calendario (fecha local), sumando sus montos.
 * Retorna [{ date, amount }] ordenado cronológicamente ascendente.
 */
export function groupTradesByDay(trades) {
  const map = new Map();
  for (const t of trades) {
    const d = new Date(t.date);
    const key = `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
    if (!map.has(key)) {
      map.set(key, { date: t.date, amount: 0 });
    }
    map.get(key).amount += t.amount;
  }
  return Array.from(map.values()).sort((a, b) => new Date(a.date) - new Date(b.date));
}

export function getProfitableDays(trades) {
  return groupTradesByDay(trades).filter((d) => d.amount > 0);
}

export function getBestDay(trades) {
  const profitable = getProfitableDays(trades);
  return profitable.length ? Math.max(...profitable.map((d) => d.amount)) : 0;
}

/**
 * % de consistencia = mejor día (agrupado) / objetivo de profit (fijo).
 * Es decir, ningún día individual puede representar más de X% de la
 * meta total del challenge. Retorna null si todavía no hay días rentables.
 */
export function getConsistencyPct(trades, profitTarget) {
  const bestDay = getBestDay(trades);
  if (bestDay <= 0 || !profitTarget) return null;
  return (bestDay / profitTarget) * 100;
}

export function isConsistencyCompliant(trades, consistencyLimitPct, profitTarget) {
  if (!consistencyLimitPct) return true; // sin regla configurada
  const pct = getConsistencyPct(trades, profitTarget);
  if (pct === null) return true;
  return pct <= consistencyLimitPct;
}

/**
 * Drawdown máximo alcanzado (peak-to-trough) sobre la curva de balance
 * acumulado por día. Sirve tanto para drawdown estático como referencia
 * visual en trailing (el cálculo exacto de trailing depende de reglas
 * propias de cada firm y puede refinarse por challenge más adelante).
 */
export function getMaxDrawdownUsed(trades) {
  const days = groupTradesByDay(trades); // ya viene ordenado ascendente
  let running = 0;
  let peak = 0;
  let maxDD = 0;
  for (const d of days) {
    running += d.amount;
    if (running > peak) peak = running;
    const dd = peak - running;
    if (dd > maxDD) maxDD = dd;
  }
  return maxDD;
}

export function isDrawdownBreached(trades, maxDrawdown) {
  return getMaxDrawdownUsed(trades) >= maxDrawdown;
}

export function getTargetProgressPct(trades, profitTarget) {
  if (!profitTarget) return 0;
  const total = getTotalProfit(trades);
  return Math.min(Math.max((total / profitTarget) * 100, 0), 100);
}

/**
 * Resumen completo de un challenge — lo que consume el dashboard.
 */
export function getChallengeSummary(challenge) {
  const { trades, profitTarget, maxDrawdown, consistencyLimitPct, minProfitableDays } = challenge;
  const totalProfit = getTotalProfit(trades);
  const profitableDays = getProfitableDays(trades);
  const consistencyPct = getConsistencyPct(trades, profitTarget);

  return {
    totalProfit,
    tradesCount: trades.length,
    daysCount: groupTradesByDay(trades).length,
    profitableDaysCount: profitableDays.length,
    minProfitableDays,
    meetsMinDays: profitableDays.length >= minProfitableDays,
    bestDay: getBestDay(trades),
    consistencyPct,
    isConsistencyCompliant: isConsistencyCompliant(trades, consistencyLimitPct, profitTarget),
    maxDrawdownUsed: getMaxDrawdownUsed(trades),
    maxDrawdown,
    drawdownBreached: isDrawdownBreached(trades, maxDrawdown),
    targetProgressPct: getTargetProgressPct(trades, profitTarget),
    targetReached: totalProfit >= profitTarget,
  };
}
