import React, { useMemo, useState } from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import IconButton from '../components/IconButton';
import FieldLabel from '../components/FieldLabel';
import EditTradeModal from '../components/EditTradeModal';
import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';
import { groupTradesByDay } from '../utils/calculations';
import { formatDate, formatMoney } from '../utils/format';

function isSameCalendarDay(a, b) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

export default function CalendarScreen() {
  const navigation = useNavigation();
  const { activeChallenge, updateTrade, deleteTrade } = useChallenges();
  const [monthCursor, setMonthCursor] = useState(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });
  const [selectedDate, setSelectedDate] = useState(null);
  const [editingTrade, setEditingTrade] = useState(null);

  const trades = activeChallenge?.trades ?? [];

  const dayTotalsMap = useMemo(() => {
    const map = new Map();
    for (const d of groupTradesByDay(trades)) {
      const date = new Date(d.date);
      map.set(`${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`, d.amount);
    }
    return map;
  }, [trades]);

  const year = monthCursor.getFullYear();
  const month = monthCursor.getMonth();
  const firstWeekday = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();

  const cells = [];
  for (let i = 0; i < firstWeekday; i++) cells.push(null);
  for (let day = 1; day <= daysInMonth; day++) cells.push(day);

  const goPrevMonth = () => {
    setSelectedDate(null);
    setMonthCursor(new Date(year, month - 1, 1));
  };
  const goNextMonth = () => {
    setSelectedDate(null);
    setMonthCursor(new Date(year, month + 1, 1));
  };

  const selectedDayTrades = selectedDate
    ? trades.filter((t) => isSameCalendarDay(new Date(t.date), selectedDate)).reverse()
    : [];
  const selectedDayTotal = selectedDayTrades.reduce((s, t) => s + t.amount, 0);

  if (!activeChallenge) {
    return (
      <Screen>
        <ScreenHeader title={strings.calendar.title} onBack={() => navigation.goBack()} />
        <Text style={styles.emptyText}>No hay ninguna cuenta activa.</Text>
      </Screen>
    );
  }

  return (
    <Screen>
      <ScreenHeader
        eyebrow={activeChallenge.name.toUpperCase()}
        title={strings.calendar.title}
        onBack={() => navigation.goBack()}
      />

      <View style={styles.monthRow}>
        <IconButton name="chevron-back" onPress={goPrevMonth} />
        <Text style={styles.monthLabel}>
          {strings.calendar.months[month]} {year}
        </Text>
        <IconButton name="chevron-forward" onPress={goNextMonth} />
      </View>

      <View style={styles.weekdayRow}>
        {strings.calendar.weekdays.map((w, i) => (
          <Text key={i} style={styles.weekdayLabel}>
            {w}
          </Text>
        ))}
      </View>

      <View style={styles.grid}>
        {cells.map((day, idx) => {
          if (day === null) return <View key={`blank-${idx}`} style={styles.cell} />;
          const cellDate = new Date(year, month, day);
          const key = `${year}-${month}-${day}`;
          const amount = dayTotalsMap.get(key);
          const hasData = amount !== undefined;
          const isSelected = selectedDate && isSameCalendarDay(selectedDate, cellDate);
          return (
            <TouchableOpacity
              key={key}
              style={[
                styles.cell,
                hasData && (amount >= 0 ? styles.cellPositive : styles.cellNegative),
                isSelected && styles.cellSelected,
              ]}
              disabled={!hasData}
              onPress={() => setSelectedDate(cellDate)}
            >
              <Text style={styles.cellDay}>{day}</Text>
              {hasData ? (
                <Text
                  style={[styles.cellAmount, { color: amount >= 0 ? theme.colors.positive : theme.colors.negative }]}
                  numberOfLines={1}
                >
                  {amount >= 0 ? '+' : ''}
                  {Math.round(amount)}
                </Text>
              ) : null}
            </TouchableOpacity>
          );
        })}
      </View>

      {selectedDate ? (
        <>
          <View style={styles.divider} />
          <View style={styles.dayDetailHeader}>
            <FieldLabel>{formatDate(selectedDate.toISOString())}</FieldLabel>
            <Text style={[styles.dayDetailTotal, { color: selectedDayTotal >= 0 ? theme.colors.positive : theme.colors.negative }]}>
              {selectedDayTotal >= 0 ? '+' : ''}
              {formatMoney(selectedDayTotal)}
            </Text>
          </View>

          {selectedDayTrades.length === 0 ? (
            <Text style={styles.emptyText}>{strings.calendar.noTradesForDay}</Text>
          ) : (
            <View style={styles.tradesList}>
              {selectedDayTrades.map((t) => (
                <TouchableOpacity key={t.id} style={styles.tradeRow} onPress={() => setEditingTrade(t)}>
                  <Text style={styles.tradeLabel}>Trade</Text>
                  <Text style={[styles.tradeAmount, { color: t.amount >= 0 ? theme.colors.positive : theme.colors.negative }]}>
                    {t.amount >= 0 ? '+' : ''}
                    {formatMoney(t.amount)}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          )}
        </>
      ) : null}

      <EditTradeModal
        visible={!!editingTrade}
        trade={editingTrade}
        onClose={() => setEditingTrade(null)}
        onSave={(updates) => updateTrade(activeChallenge.id, editingTrade.id, updates)}
        onDelete={() => deleteTrade(activeChallenge.id, editingTrade.id)}
      />
    </Screen>
  );
}

const CELL_GAP = 4;

const styles = StyleSheet.create({
  monthRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: theme.spacing(4),
  },
  monthLabel: {
    fontSize: 16,
    fontWeight: '700',
    color: theme.colors.textPrimary,
  },
  weekdayRow: {
    flexDirection: 'row',
    marginBottom: 6,
  },
  weekdayLabel: {
    flex: 1,
    textAlign: 'center',
    fontSize: 11,
    fontWeight: '600',
    color: theme.colors.textMuted,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  cell: {
    width: `${100 / 7}%`,
    aspectRatio: 1,
    padding: CELL_GAP / 2,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: theme.radius.sm,
  },
  cellPositive: {
    backgroundColor: 'rgba(74,222,128,0.12)',
  },
  cellNegative: {
    backgroundColor: 'rgba(248,113,113,0.12)',
  },
  cellSelected: {
    borderWidth: 1.5,
    borderColor: theme.colors.accent,
  },
  cellDay: {
    fontSize: 12,
    color: theme.colors.textPrimary,
    fontWeight: '600',
  },
  cellAmount: {
    fontSize: 9,
    fontWeight: '700',
    marginTop: 1,
  },
  divider: {
    height: 1,
    backgroundColor: theme.colors.border,
    marginVertical: theme.spacing(5),
  },
  dayDetailHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: theme.spacing(3),
  },
  dayDetailTotal: {
    fontSize: 16,
    fontWeight: '800',
  },
  tradesList: { gap: 8 },
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
  tradeLabel: { fontSize: 13, color: theme.colors.textSecondary },
  tradeAmount: { fontWeight: '700' },
  emptyText: { color: theme.colors.textSecondary, fontSize: 13 },
});
