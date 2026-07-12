import React, { useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import FieldLabel from '../components/FieldLabel';
import PresetCard from '../components/PresetCard';
import SegmentedControl from '../components/SegmentedControl';
import DonutChart from '../components/DonutChart';
import Sparkline from '../components/Sparkline';
import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';
import { usePremium } from '../context/PremiumContext';
import { getTradeStats, getCumulativeCurve } from '../utils/calculations';
import { formatMoney } from '../utils/format';

const PERIOD_OPTIONS = [
  { label: strings.analytics.periodWeek, value: 'week' },
  { label: strings.analytics.periodMonth, value: 'month' },
  { label: strings.analytics.periodYear, value: 'year' },
  { label: strings.analytics.periodAll, value: 'all' },
];

function getPeriodStart(period) {
  const now = new Date();
  if (period === 'week') {
    const dayOfWeek = now.getDay();
    const diffToMonday = (dayOfWeek + 6) % 7;
    const monday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - diffToMonday);
    monday.setHours(0, 0, 0, 0);
    return monday;
  }
  if (period === 'month') {
    return new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0, 0);
  }
  if (period === 'year') {
    return new Date(now.getFullYear(), 0, 1, 0, 0, 0, 0);
  }
  return null;
}

function filterTradesByPeriod(trades, period) {
  const start = getPeriodStart(period);
  if (!start) return trades;
  return trades.filter((t) => new Date(t.date) >= start);
}

export default function AnalyticsScreen() {
  const navigation = useNavigation();
  const { challenges, activeChallenge, setActiveChallenge } = useChallenges();
  const { isPremium } = usePremium();
  const [period, setPeriod] = useState('month');
  const lockedPeriods = isPremium ? [] : ['year', 'all'];

  const challenge = activeChallenge;

  const trades = useMemo(() => filterTradesByPeriod(challenge?.trades ?? [], period), [challenge, period]);
  const stats = useMemo(() => getTradeStats(trades), [trades]);
  const curve = useMemo(() => getCumulativeCurve(trades), [trades]);

  if (!challenge) {
    return (
      <Screen>
        <ScreenHeader title={strings.analytics.title} onBack={() => navigation.goBack()} />
        <Text style={styles.emptyText}>No hay ninguna cuenta activa.</Text>
      </Screen>
    );
  }

  const grossTotal = stats.totalProfit + Math.abs(stats.totalLoss);
  const pnlPct = grossTotal ? (stats.totalProfit / grossTotal) * 100 : null;
  const winRatePct = stats.totalTrades ? stats.winRate : null;
  const pfTotal = stats.avgWin + Math.abs(stats.avgLoss);
  const pfPct = pfTotal ? (stats.avgWin / pfTotal) * 100 : null;

  return (
    <Screen>
      <ScreenHeader
        eyebrow={challenge.name.toUpperCase()}
        title={strings.analytics.title}
        onBack={() => navigation.goBack()}
      />

      {challenges.length > 1 && (
        <View style={styles.accountList}>
          <FieldLabel>{strings.analytics.accountsLabel}</FieldLabel>
          {challenges.map((c) => (
            <PresetCard
              key={c.id}
              label={c.name}
              active={c.id === challenge.id}
              onPress={() => setActiveChallenge(c.id)}
            />
          ))}
        </View>
      )}

      <SegmentedControl
        options={PERIOD_OPTIONS}
        value={period}
        onChange={setPeriod}
        style={styles.periodControl}
        lockedValues={lockedPeriods}
        onLockedPress={() => navigation.navigate('Paywall')}
      />

      {stats.totalTrades === 0 ? (
        <Text style={styles.emptyText}>{strings.analytics.emptyState}</Text>
      ) : (
        <>
          {/* Drawdown */}
          <View style={styles.panel}>
            <View style={styles.panelHeadRow}>
              <Text style={styles.panelTitle}>{strings.analytics.drawdownTitle}</Text>
              <Text style={styles.link} onPress={() => navigation.navigate('Calendar')}>
                {strings.analytics.viewCalendar}
              </Text>
            </View>
            <Sparkline data={curve} color={theme.colors.positive} />
          </View>

          {/* P&L */}
          <View style={styles.panel}>
            <Text style={styles.cardTitle}>{strings.analytics.pnlTitle}</Text>
            <StatLine label={strings.analytics.totalProfitLabel} value={formatMoney(stats.totalProfit)} />
            <StatLine label={strings.analytics.totalLossLabel} value={formatMoney(stats.totalLoss)} />
            <View style={styles.donutWrap}>
              <DonutChart pct={pnlPct} />
            </View>
            <Legend gainLabel={strings.analytics.legendGain} lossLabel={strings.analytics.legendLoss} />
            <Headline label={strings.analytics.netPnlLabel} value={formatMoney(stats.netPnl)} positive={stats.netPnl >= 0} />
          </View>

          {/* Win rate */}
          <View style={styles.panel}>
            <Text style={styles.cardTitle}>{strings.analytics.winRateTitle}</Text>
            <StatLine label={strings.analytics.totalTradesLabel} value={String(stats.totalTrades)} />
            <StatLine label={strings.analytics.winTradesLabel} value={String(stats.winTrades)} />
            <StatLine label={strings.analytics.lossTradesLabel} value={String(stats.lossTrades)} />
            <View style={styles.donutWrap}>
              <DonutChart pct={winRatePct} />
            </View>
            <Legend gainLabel={strings.analytics.legendWin} lossLabel={strings.analytics.legendLossTrades} />
            <Headline label={strings.analytics.winRateLabel} value={`${stats.winRate.toFixed(1)}%`} positive />
          </View>

          {/* Profit factor */}
          <View style={styles.panel}>
            <Text style={styles.cardTitle}>{strings.analytics.profitFactorTitle}</Text>
            <StatLine label={strings.analytics.avgWinLabel} value={formatMoney(stats.avgWin)} />
            <StatLine label={strings.analytics.avgLossLabel} value={formatMoney(stats.avgLoss)} />
            <View style={styles.donutWrap}>
              <DonutChart pct={pfPct} />
            </View>
            <Legend gainLabel={strings.analytics.legendAvgWin} lossLabel={strings.analytics.legendAvgLoss} />
            <View style={styles.metricsRow}>
              <View style={styles.metricBox}>
                <Text style={styles.metricLabel}>{strings.analytics.profitFactorLabel}</Text>
                <Text style={[styles.metricValue, { color: stats.profitFactor >= 1 ? theme.colors.positive : theme.colors.negative }]}>
                  {stats.profitFactor.toFixed(2)}
                </Text>
              </View>
              <View style={styles.metricBox}>
                <Text style={styles.metricLabel}>{strings.analytics.expectancyLabel}</Text>
                <Text style={[styles.metricValue, { color: stats.expectancy >= 0 ? theme.colors.positive : theme.colors.negative }]}>
                  {formatMoney(stats.expectancy)}
                </Text>
              </View>
            </View>
          </View>
        </>
      )}
    </Screen>
  );
}

function StatLine({ label, value }) {
  return (
    <Text style={styles.statLine}>
      {label}: <Text style={styles.statLineValue}>{value}</Text>
    </Text>
  );
}

function Legend({ gainLabel, lossLabel }) {
  return (
    <View style={styles.legend}>
      <View style={styles.legendItem}>
        <View style={[styles.dot, { backgroundColor: theme.colors.positive }]} />
        <Text style={styles.legendText}>{gainLabel}</Text>
      </View>
      <View style={styles.legendItem}>
        <View style={[styles.dot, { backgroundColor: theme.colors.negative }]} />
        <Text style={styles.legendText}>{lossLabel}</Text>
      </View>
    </View>
  );
}

function Headline({ label, value, positive }) {
  return (
    <View style={styles.headline}>
      <Text style={styles.headlineLabel}>{label}</Text>
      <Text style={[styles.headlineValue, { color: positive ? theme.colors.positive : theme.colors.negative }]}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  accountList: { marginBottom: theme.spacing(3) },
  periodControl: { marginBottom: theme.spacing(4) },
  panel: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.lg,
    padding: theme.spacing(4),
    marginBottom: theme.spacing(4),
  },
  panelHeadRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: theme.spacing(3),
  },
  panelTitle: { fontSize: 15, fontWeight: '700', color: theme.colors.textPrimary },
  link: { color: theme.colors.accent, fontSize: 12, fontWeight: '600' },
  cardTitle: { fontSize: 15, fontWeight: '700', color: theme.colors.textPrimary, marginBottom: theme.spacing(2) },
  statLine: { fontSize: 12.5, color: theme.colors.textSecondary, marginVertical: 2 },
  statLineValue: { color: theme.colors.textPrimary, fontWeight: '600' },
  donutWrap: { alignItems: 'center', marginVertical: theme.spacing(4) },
  legend: { flexDirection: 'row', justifyContent: 'center', gap: 20, marginBottom: 4 },
  legendItem: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  dot: { width: 9, height: 9, borderRadius: 5 },
  legendText: { fontSize: 12, color: theme.colors.textSecondary },
  headline: { alignItems: 'center', marginTop: theme.spacing(3) },
  headlineLabel: { fontSize: 12, color: theme.colors.textSecondary, marginBottom: 4 },
  headlineValue: { fontSize: 26, fontWeight: '700' },
  metricsRow: { flexDirection: 'row', gap: 12, marginTop: theme.spacing(3) },
  metricBox: {
    flex: 1,
    alignItems: 'center',
    backgroundColor: theme.colors.background,
    borderRadius: theme.radius.md,
    paddingVertical: 12,
  },
  metricLabel: { fontSize: 10.5, color: theme.colors.textSecondary, marginBottom: 5 },
  metricValue: { fontSize: 17, fontWeight: '700' },
  emptyText: { color: theme.colors.textSecondary, fontSize: 13, textAlign: 'center', marginTop: theme.spacing(4) },
});
