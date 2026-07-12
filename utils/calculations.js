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
 * Ganancia acumulada máxima alcanzada (high-water mark) a lo largo de la
 * curva de balance por día. Es la referencia que usa el drawdown trailing.
 */
export function getPeakProfit(trades) {
  const days = groupTradesByDay(trades);
  let running = 0;
  let peak = 0;
  for (const d of days) {
    running += d.amount;
    if (running > peak) peak = running;
  }
  return peak;
}

/**
 * Drawdown máximo alcanzado, según el tipo de cuenta:
 * - 'trailing': peak-to-trough — el piso SUBE con el balance más alto
 *   alcanzado. Romper por debajo del último máximo cuenta como drawdown,
 *   aunque el balance total siga siendo positivo.
 * - 'static': el piso queda FIJO en el balance inicial de la cuenta — solo
 *   cuenta cuánto has caído por debajo de ese punto de partida, sin
 *   importar qué tan alto hayas llegado.
 */
export function getMaxDrawdownUsed(trades, drawdownType = 'trailing') {
  const days = groupTradesByDay(trades); // ya viene ordenado ascendente

  if (drawdownType === 'static') {
    let running = 0;
    let maxDD = 0;
    for (const d of days) {
      running += d.amount;
      const dd = Math.max(0, -running);
      if (dd > maxDD) maxDD = dd;
    }
    return maxDD;
  }

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

export function isDrawdownBreached(trades, maxDrawdown, drawdownType = 'trailing') {
  return getMaxDrawdownUsed(trades, drawdownType) >= maxDrawdown;
}

/**
 * Piso actual de drawdown, en balance de cuenta (accountSize + profit).
 * En trailing sube con el máximo histórico; en estático queda fijo.
 */
export function getDrawdownFloor(trades, accountSize, maxDrawdown, drawdownType = 'trailing') {
  const anchor = drawdownType === 'static' ? 0 : getPeakProfit(trades);
  return accountSize + anchor - maxDrawdown;
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
  const { trades, accountSize, profitTarget, maxDrawdown, drawdownType, consistencyLimitPct, minProfitableDays } = challenge;
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
    maxDrawdownUsed: getMaxDrawdownUsed(trades, drawdownType),
    maxDrawdown,
    drawdownFloor: getDrawdownFloor(trades, accountSize, maxDrawdown, drawdownType),
    drawdownBreached: isDrawdownBreached(trades, maxDrawdown, drawdownType),
    targetProgressPct: getTargetProgressPct(trades, profitTarget),
    targetReached: totalProfit >= profitTarget,
  };
}

/**
 * Estadísticas de trading clásicas (win rate, profit factor, expectativa),
 * calculadas por TRADE individual — no por día — a diferencia de las
 * reglas de consistencia/drawdown de arriba.
 */
export function getTradeStats(trades) {
  const wins = trades.filter((t) => t.amount > 0);
  const losses = trades.filter((t) => t.amount < 0);
  const totalProfit = wins.reduce((s, t) => s + t.amount, 0);
  const totalLoss = losses.reduce((s, t) => s + t.amount, 0);
  const winRate = trades.length ? (wins.length / trades.length) * 100 : 0;
  const avgWin = wins.length ? totalProfit / wins.length : 0;
  const avgLoss = losses.length ? totalLoss / losses.length : 0;
  const profitFactor = totalLoss !== 0 ? Math.abs(totalProfit / totalLoss) : 0;
  const expectancy = trades.length
    ? (wins.length / trades.length) * avgWin + (losses.length / trades.length) * avgLoss
    : 0;

  return {
    totalTrades: trades.length,
    winTrades: wins.length,
    lossTrades: losses.length,
    totalProfit,
    totalLoss,
    netPnl: totalProfit + totalLoss,
    winRate,
    avgWin,
    avgLoss,
    profitFactor,
    expectancy,
  };
}

/**
 * Curva de equity acumulado a partir de una lista de trades, en orden
 * cronológico. Empieza en 0. Útil para graficar drawdown/progreso.
 */
export function getCumulativeCurve(trades) {
  const sorted = [...trades].sort((a, b) => new Date(a.date) - new Date(b.date));
  let running = 0;
  return [0, ...sorted.map((t) => (running += t.amount))];
}
