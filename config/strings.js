/**
 * Textos de UI centralizados en español (mercado objetivo inicial V1).
 * Mantener todo el copy visible al usuario aquí para poder
 * internacionalizar más adelante sin tocar las pantallas.
 */
export const strings = {
  tabs: {
    dashboard: 'Dashboard',
    settings: 'Configuración',
  },
  onboarding: {
    eyebrow: 'NUEVO CHALLENGE',
    title: 'Configura tu cuenta',
    subtitle: 'Define los parámetros de tu evaluación o cuenta fondeada',
    presetLabel: 'Elige un punto de partida',
    nameLabel: 'Nombre de la cuenta',
    namePlaceholder: 'Ej. Tradeify Select 100k',
    accountSizeLabel: 'Tamaño de cuenta',
    profitTargetLabel: 'Objetivo de profit',
    maxDrawdownLabel: 'Drawdown máximo',
    minProfitableDaysLabel: 'Días mín. rentables',
    drawdownTypeLabel: 'Tipo de drawdown',
    drawdownTypeStatic: 'Estático',
    drawdownTypeTrailing: 'Trailing',
    consistencyLabel: 'Regla de consistencia',
    consistencyNone: 'Sin regla',
    consistencyWithLimit: 'Con límite',
    consistencyPctLabel: '% máximo por día',
    submit: 'Crear cuenta',
    hint: 'Podrás editar todos estos valores después desde Configuración.',
    presetCustomLabel: 'Personalizado',
  },
  calendar: {
    title: 'Calendario',
    weekdays: ['D', 'L', 'M', 'M', 'J', 'V', 'S'],
    months: [
      'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
      'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
    ],
    noTradesForDay: 'Sin trades este día',
    dayTotal: 'Total del día',
  },
};
