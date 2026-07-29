import React, { useState } from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import MetricCard from '../components/MetricCard';
import PrimaryButton from '../components/PrimaryButton';
import FieldLabel from '../components/FieldLabel';
import IconButton from '../components/IconButton';
import AddDayModal from '../components/AddDayModal';
import EditTradeModal from '../components/EditTradeModal';
import BreachModal from '../components/BreachModal';
import AccountSwitcherModal from '../components/AccountSwitcherModal';
import AdBanner from '../components/AdBanner';
import { theme } from '../config/theme';
import { useStrings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';
import { usePreferences } from '../context/PreferencesContext';
import { getChallengeSummary } from '../utils/calculations';
import { formatDate, formatMoney } from '../utils/format';

export default function DashboardScreen() {
  const navigation = useNavigation();
  const strings = useStrings();
  const {
    activeChallenge,
    challenges,
    addTrade,
    updateTrade,
    deleteTrade,
    setActiveChallenge,
    restartEvaluation,
    acknowledgeBreach,
    deleteChallenge,
  } = useChallenges();
  const { hidePnl } = usePreferences();
  const [modalVisible, setModalVisible] = useState(false);
  const [editingTrade, setEditingTrade] = useState(null);
  const [switcherVisible, setSwitcherVisible] = useState(false);
  const money = (value) => (hidePnl ? '•••••' : formatMoney(value));

  if (!activeChallenge) {
    return (
      <Screen>
        <ScreenHeader title={strings.tabs.dashboard} />
        <Text style={styles.emptyText}>{strings.common.noActiveAccount}</Text>
      </Screen>
    );
  }

  const summary = getChallengeSummary(activeChallenge);
  const limit = activeChallenge.consistencyLimitPct;
  const trades = activeChallenge.trades;
  const isFunded = activeChallenge.status === 'funded';
  const evaluationPassed =
    summary.targetReached && summary.isConsistencyCompliant && !summary.drawdownBreached && summary.meetsMinDays;
  const showUpgradeCta = evaluationPassed && !isFunded;

  const handleAddTrade = (amount, challengeIds, date) => {
    challengeIds.forEach((id) => addTrade(id, { amount, date }));
  };

  const handleSaveTrade = (updates) => {
    updateTrade(activeChallenge.id, editingTrade.id, updates);
  };

  const handleDeleteTrade = () => {
    deleteTrade(activeChallenge.id, editingTrade.id);
  };

  const showBreachModal = summary.drawdownBreached && !activeChallenge.breachAcknowledged;

  const handleRestart = () => restartEvaluation(activeChallenge.id);
  const handleSaveBreach = () => acknowledgeBreach(activeChallenge.id);
  const handleDeleteBreach = () => {
    const wasLast = challenges.length <= 1;
    deleteChallenge(activeChallenge.id);
    if (wasLast) {
      navigation.reset({ index: 0, routes: [{ name: 'Onboarding' }] });
    }
  };

  return (
    <Screen>
      <ScreenHeader
        eyebrow={`${activeChallenge.name.toUpperCase()}${isFunded ? ` · ${strings.accountStatus.badge}` : ''}`}
        title={strings.tabs.dashboard}
        onEyebrowPress={challenges.length > 1 ? () => setSwitcherVisible(true) : undefined}
        right={
          <View style={styles.headerActions}>
            <IconButton name="stats-chart-outline" onPress={() => navigation.navigate('Analytics')} />
            <IconButton name="calendar-outline" onPress={() => navigation.navigate('Calendar')} />
          </View>
        }
      />

      {showUpgradeCta && (
        <View style={styles.upgradeCard}>
          <Ionicons name="trophy" size={24} color={theme.colors.warning} />
          <View style={styles.upgradeTextWrap}>
            <Text style={styles.upgradeTitle}>{strings.accountStatus.completedTitle}</Text>
            <Text style={styles.upgradeBody}>{strings.accountStatus.completedBody}</Text>
          </View>
        </View>
      )}
      {showUpgradeCta && (
        <PrimaryButton
          label={strings.accountStatus.upgradeCta}
          onPress={() => navigation.navigate('Settings')}
          style={styles.upgradeButton}
        />
      )}

      <View style={[styles.statusCard, summary.isConsistencyCompliant ? styles.statusOk : styles.statusBad]}>
        <Ionicons
          name={summary.isConsistencyCompliant ? 'checkmark-circle' : 'warning'}
          size={26}
          color={summary.isConsistencyCompliant ? theme.colors.positive : theme.colors.negative}
        />
        <View style={styles.statusTextWrap}>
          <Text style={styles.statusPct}>
            {limit && summary.consistencyPct !== null ? `${summary.consistencyPct.toFixed(1)}%` : '—'}
          </Text>
          <Text style={styles.statusLabel}>
            {!limit
              ? strings.dashboard.noConsistencyRule
              : summary.isConsistencyCompliant
              ? strings.dashboard.withinLimit.replace('{limit}', limit)
              : strings.dashboard.overLimit.replace('{limit}', limit)}
          </Text>
        </View>
      </View>

      <View style={styles.grid}>
        <MetricCard label={strings.dashboard.totalProfit} value={money(summary.totalProfit)} accent={theme.colors.positive} />
        <MetricCard
          label={strings.dashboard.targetProgress}
          value={`${summary.targetProgressPct.toFixed(0)}%`}
          subValue={`${money(summary.totalProfit)} / ${money(activeChallenge.profitTarget)}`}
          accent={theme.colors.accent}
          barPct={summary.targetProgressPct}
        />
        <MetricCard label={strings.dashboard.bestDay} value={money(summary.bestDay)} accent={theme.colors.warning} />
        <MetricCard
          label={strings.dashboard.drawdownUsed}
          value={`${money(summary.maxDrawdownUsed)} / ${money(summary.maxDrawdown)}`}
          subValue={
            activeChallenge.drawdownType === 'trailing'
              ? strings.dashboard.floorLabel.replace('{value}', money(summary.drawdownFloor))
              : undefined
          }
          accent={summary.maxDrawdownUsed > summary.maxDrawdown * 0.75 ? theme.colors.negative : theme.colors.textSecondary}
        />
      </View>

      <View style={styles.infoBar}>
        <Text style={styles.infoText}>
          {strings.dashboard.daysOperated} <Text style={styles.infoStrong}>{summary.daysCount}</Text>
        </Text>
        <Text style={styles.infoDot}>·</Text>
        <Text style={styles.infoText}>
          {strings.dashboard.profitable} <Text style={styles.infoStrong}>{summary.profitableDaysCount}</Text> / {summary.minProfitableDays} {strings.dashboard.minSuffix}
        </Text>
      </View>

      {trades.length > 0 ? (
        <>
          <FieldLabel style={styles.tradesLabel}>{strings.dashboard.recentTrades}</FieldLabel>
          <View style={styles.tradesList}>
            {[...trades].reverse().map((t) => (
              <TouchableOpacity key={t.id} style={styles.tradeRow} onPress={() => setEditingTrade(t)}>
                <View style={styles.tradeLeft}>
                  <Ionicons
                    name={t.amount >= 0 ? 'trending-up' : 'trending-down'}
                    size={15}
                    color={t.amount >= 0 ? theme.colors.positive : theme.colors.negative}
                  />
                  <Text style={styles.tradeDate}>{formatDate(t.date)}</Text>
                </View>
                <Text style={[styles.tradeAmount, { color: t.amount >= 0 ? theme.colors.positive : theme.colors.negative }]}>
                  {hidePnl ? '•••••' : `${t.amount >= 0 ? '+' : ''}${formatMoney(t.amount)}`}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
        </>
      ) : null}

      <PrimaryButton label={strings.dashboard.registerDay} onPress={() => setModalVisible(true)} style={styles.registerButton} />

      <AdBanner />

      <AddDayModal
        visible={modalVisible}
        onClose={() => setModalVisible(false)}
        onSubmit={handleAddTrade}
        challenges={challenges}
        activeChallengeId={activeChallenge.id}
      />

      <EditTradeModal
        visible={!!editingTrade}
        trade={editingTrade}
        onClose={() => setEditingTrade(null)}
        onSave={handleSaveTrade}
        onDelete={handleDeleteTrade}
      />

      <AccountSwitcherModal
        visible={switcherVisible}
        challenges={challenges}
        activeChallengeId={activeChallenge.id}
        onSelect={setActiveChallenge}
        onClose={() => setSwitcherVisible(false)}
      />

      <BreachModal
        visible={showBreachModal}
        onRestart={handleRestart}
        onSave={handleSaveBreach}
        onDelete={handleDeleteBreach}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  headerActions: { flexDirection: 'row', gap: 8 },
  upgradeCard: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: 'rgba(240,180,41,0.1)',
    borderWidth: 1,
    borderColor: 'rgba(240,180,41,0.35)',
    borderRadius: theme.radius.lg,
    padding: theme.spacing(4),
    marginBottom: theme.spacing(3),
  },
  upgradeTextWrap: { flex: 1 },
  upgradeTitle: { fontSize: 14.5, fontWeight: '700', color: theme.colors.textPrimary },
  upgradeBody: { fontSize: 12, color: theme.colors.textSecondary, marginTop: 2, lineHeight: 16 },
  upgradeButton: { marginBottom: theme.spacing(4) },
  statusCard: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    paddingVertical: 16,
    paddingHorizontal: 18,
    borderRadius: theme.radius.lg,
    borderWidth: 1,
    marginBottom: theme.spacing(4),
  },
  statusOk: {
    backgroundColor: 'rgba(74,222,128,0.08)',
    borderColor: 'rgba(74,222,128,0.25)',
  },
  statusBad: {
    backgroundColor: 'rgba(248,113,113,0.08)',
    borderColor: 'rgba(248,113,113,0.25)',
  },
  statusTextWrap: { flex: 1 },
  statusPct: { fontSize: 24, fontWeight: '800', color: theme.colors.textPrimary },
  statusLabel: { fontSize: 12.5, color: theme.colors.textSecondary, marginTop: 4 },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 10,
    marginBottom: theme.spacing(4),
  },
  infoBar: {
    flexDirection: 'row',
    gap: 8,
    paddingVertical: 10,
    paddingHorizontal: 14,
    backgroundColor: theme.colors.surface,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: theme.colors.border,
    marginBottom: theme.spacing(4),
  },
  infoText: { fontSize: 12.5, color: theme.colors.textSecondary },
  infoStrong: { fontWeight: '700', color: theme.colors.textPrimary },
  infoDot: { color: theme.colors.border },
  tradesLabel: { marginTop: 4 },
  tradesList: { gap: 8, marginBottom: theme.spacing(2) },
  tradeRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    paddingVertical: 10,
    paddingHorizontal: 14,
  },
  tradeLeft: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  tradeDate: { fontSize: 13, color: theme.colors.textSecondary },
  tradeAmount: { fontWeight: '700' },
  registerButton: { marginTop: theme.spacing(4) },
  emptyText: { color: theme.colors.textSecondary, fontSize: 13 },
});
