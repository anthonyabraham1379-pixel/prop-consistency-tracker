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
import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';
import { getChallengeSummary } from '../utils/calculations';
import { formatDate, formatMoney } from '../utils/format';

export default function DashboardScreen() {
  const navigation = useNavigation();
  const { activeChallenge, challenges, addTrade, updateTrade, deleteTrade } = useChallenges();
  const [modalVisible, setModalVisible] = useState(false);
  const [editingTrade, setEditingTrade] = useState(null);

  if (!activeChallenge) {
    return (
      <Screen>
        <ScreenHeader title={strings.tabs.dashboard} />
        <Text style={styles.emptyText}>No hay ninguna cuenta activa.</Text>
      </Screen>
    );
  }

  const summary = getChallengeSummary(activeChallenge);
  const limit = activeChallenge.consistencyLimitPct;
  const trades = activeChallenge.trades;

  const handleAddTrade = (amount, challengeIds, date) => {
    challengeIds.forEach((id) => addTrade(id, { amount, date }));
  };

  const handleSaveTrade = (updates) => {
    updateTrade(activeChallenge.id, editingTrade.id, updates);
  };

  const handleDeleteTrade = () => {
    deleteTrade(activeChallenge.id, editingTrade.id);
  };

  return (
    <Screen>
      <ScreenHeader
        eyebrow={activeChallenge.name.toUpperCase()}
        title={strings.tabs.dashboard}
        right={
          <View style={styles.headerActions}>
            <IconButton name="stats-chart-outline" onPress={() => navigation.navigate('Analytics')} />
            <IconButton name="calendar-outline" onPress={() => navigation.navigate('Calendar')} />
          </View>
        }
      />

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
              ? 'Sin regla de consistencia configurada'
              : summary.isConsistencyCompliant
              ? `Cumples el límite de ${limit}%`
              : `Excede el límite de ${limit}%`}
          </Text>
        </View>
      </View>

      <View style={styles.grid}>
        <MetricCard label="Ganancia total" value={formatMoney(summary.totalProfit)} accent={theme.colors.positive} />
        <MetricCard
          label="Progreso a meta"
          value={`${summary.targetProgressPct.toFixed(0)}%`}
          subValue={`${formatMoney(summary.totalProfit)} / ${formatMoney(activeChallenge.profitTarget)}`}
          accent={theme.colors.accent}
          barPct={summary.targetProgressPct}
        />
        <MetricCard label="Mejor día" value={formatMoney(summary.bestDay)} accent={theme.colors.warning} />
        <MetricCard
          label="Drawdown usado"
          value={`${formatMoney(summary.maxDrawdownUsed)} / ${formatMoney(summary.maxDrawdown)}`}
          accent={summary.maxDrawdownUsed > summary.maxDrawdown * 0.75 ? theme.colors.negative : theme.colors.textSecondary}
        />
      </View>

      <View style={styles.infoBar}>
        <Text style={styles.infoText}>
          Días operados: <Text style={styles.infoStrong}>{summary.daysCount}</Text>
        </Text>
        <Text style={styles.infoDot}>·</Text>
        <Text style={styles.infoText}>
          Rentables: <Text style={styles.infoStrong}>{summary.profitableDaysCount}</Text> / {summary.minProfitableDays} mín.
        </Text>
      </View>

      {trades.length > 0 ? (
        <>
          <FieldLabel style={styles.tradesLabel}>Últimos trades</FieldLabel>
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
                  {t.amount >= 0 ? '+' : ''}
                  {formatMoney(t.amount)}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
        </>
      ) : null}

      <PrimaryButton label="+ Registrar día" onPress={() => setModalVisible(true)} style={styles.registerButton} />

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
    </Screen>
  );
}

const styles = StyleSheet.create({
  headerActions: { flexDirection: 'row', gap: 8 },
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
