/**
 * Plantilla vacía para un nuevo challenge/cuenta.
 * El usuario llena estos campos en el onboarding.
 */
export const EMPTY_CHALLENGE = {
  id: null,
  name: '',
  accountSize: '',
  profitTarget: '',
  maxDrawdown: '',
  drawdownType: 'static', // 'static' | 'trailing'
  consistencyLimitPct: null, // null = sin regla de consistencia
  minProfitableDays: '',
  suggestedDailyGoal: null, // se puede calcular o dejar que el usuario lo escriba
  createdAt: null,
  status: 'evaluation', // 'evaluation' | 'funded'
  archivedPhases: [], // [{ trades, archivedAt, outcome: 'funded' | 'failed' }]
  breachAcknowledged: false,
  trades: [],
};

/**
 * Presets opcionales para que el usuario arranque más rápido.
 * IMPORTANTE: estos son valores de referencia pública y cambian con el
 * tiempo — mostrar siempre un aviso de "verifica las reglas actuales de
 * tu firm" y permitir edición total. No hardcodear esto como fuente de
 * verdad permanente.
 */
export const FIRM_PRESETS = [
  {
    key: 'starter100k',
    accountSize: 100000,
    profitTarget: 6000,
    maxDrawdown: 3000,
    drawdownType: 'static',
    consistencyLimitPct: 40,
    minProfitableDays: 3,
  },
  {
    key: 'custom',
    accountSize: null,
    profitTarget: null,
    maxDrawdown: null,
    drawdownType: 'static',
    consistencyLimitPct: null,
    minProfitableDays: 1,
  },
];
