import React, { useMemo, useState } from 'react';
import { StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import IconButton from '../components/IconButton';
import FieldLabel from '../components/FieldLabel';
import PresetCard from '../components/PresetCard';
import SegmentedControl from '../components/SegmentedControl';
import EditTradeModal from '../components/EditTradeModal';
import { theme } from '../config/theme';
import { useStrings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';
import { usePremium } from '../context/PremiumContext';
import { groupTradesByDay, getTotalProfit } from '../utils/calculations';
import { formatDate, formatMoney } from '../utils/format';

function isSameCalendarDay(a, b) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

function getWeekStart(date) {
  const d = new Date(date);
  const diffToMonday = (d.getDay() + 6) % 7;
  d.setDate(d.getDate() - diffToMonday);
  d.setHours(0, 0, 0, 0);
  return d;
}

function shortDate(d) {
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
}

function getMonthTrades(trades, year, month) {
  return trades.filter((t) => {
    const d = new Date(t.date);
    return d.getFullYear() === year && d.getMonth() === month;
  });
}

export default function CalendarScreen() {
  const navigation = useNavigation();
  const strings = useStrings();
  const { challenges, activeChallenge, setActiveChallenge, updateTrade, deleteTrade } = useChallenges();
  const { isPremium } = usePremium();
  const [period, setPeriod] = useState('month');

  const PERIOD_OPTIONS = [
    { label: strings.analytics.periodWeek, value: 'week' },
    { label: strings.analytics.periodMonth, value: 'month' },
    { label: strings.analytics.periodYear, value: 'year' },
    { label: strings.analytics.periodAll, value: 'all' },
  ];
  const lockedPeriods = isPremium ? [] : ['year', 'all'];
  const [cursorDate, setCursorDate] = useState(() => new Date());
  const [selectedDate, setSelectedDate] = useState(null);
  const [editingTrade, setEditingTrade] = useState(null);

  const challenge = activeChallenge;
  const trades = challenge?.trades ?? [];

  const dayTotalsMap = useMemo(() => {
    const map = new Map();
    for (const d of groupTradesByDay(trades)) {
      const date = new Date(d.date);
      map.set(`${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`, d.amount);
    }
    return map;
  }, [trades]);

  if (!challenge) {
    return (
      <Screen>
        <ScreenHeader title={strings.calendar.title} onBack={() => navigation.goBack()} />
        <Text style={styles.emptyText}>{strings.common.noActiveAccount}</Text>
      </Screen>
    );
  }

  const year = cursorDate.getFullYear();
  const month = cursorDate.getMonth();

  const shiftCursor = (direction) => {
    setSelectedDate(null);
    setCursorDate((d) => {
      const next = new Date(d);
      if (period === 'week') next.setDate(next.getDate() + 7 * direction);
      else if (period === 'month') next.setMonth(next.getMonth() + direction);
      else if (period === 'year') next.setFullYear(next.getFullYear() + direction);
      return next;
    });
  };

  const handlePeriodChange = (value) => {
    setPeriod(value);
    setSelectedDate(null);
  };

  const goToMonth = (targetYear, targetMonth) => {
    setCursorDate(new Date(targetYear, targetMonth, 1));
    setPeriod('month');
    setSelectedDate(null);
  };

  const selectedDayTrades = selectedDate
    ? trades.filter((t) => isSameCalendarDay(new Date(t.date), selectedDate)).reverse()
    : [];
  const selectedDayTotal = selectedDayTrades.reduce((s, t) => s + t.amount, 0);

  let headerLabel = null;
  if (period === 'month') headerLabel = `${strings.calendar.months[month]} ${year}`;
  else if (period === 'year') headerLabel = String(year);
  else if (period === 'week') {
    const weekStart = getWeekStart(cursorDate);
    const weekEnd = new Date(weekStart);
    weekEnd.setDate(weekEnd.getDate() + 6);
    headerLabel = `${shortDate(weekStart)} - ${shortDate(weekEnd)}`;
  }

  return (
    <Screen>
      <ScreenHeader
        eyebrow={challenge.name.toUpperCase()}
        title={strings.calendar.title}
        onBack={() => navigation.goBack()}
      />

      {challenges.length > 1 && (
        <View style={styles.accountList}>
          <FieldLabel>{strings.calendar.accountsLabel}</FieldLabel>
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
        onChange={handlePeriodChange}
        style={styles.periodControl}
        lockedValues={lockedPeriods}
        onLockedPress={() => navigation.navigate('Paywall')}
      />

      {headerLabel && (
        <View style={styles.navRow}>
          <IconButton name="chevron-back" onPress={() => shiftCursor(-1)} />
          <Text style={styles.navLabel}>{headerLabel}</Text>
          <IconButton name="chevron-forward" onPress={() => shiftCursor(1)} />
        </View>
      )}

      {period === 'week' && (
        <WeekView
          strings={strings}
          weekStart={getWeekStart(cursorDate)}
          dayTotalsMap={dayTotalsMap}
          selectedDate={selectedDate}
          onSelectDate={setSelectedDate}
        />
      )}

      {period === 'month' && (
        <MonthGrid
          strings={strings}
          year={year}
          month={month}
          dayTotalsMap={dayTotalsMap}
          selectedDate={selectedDate}
          onSelectDate={setSelectedDate}
        />
      )}

      {period === 'year' && (
        <YearView strings={strings} year={year} trades={trades} onSelectMonth={(m) => goToMonth(year, m)} />
      )}

      {period === 'all' && <AllTimeView strings={strings} trades={trades} />}

      {(period === 'week' || period === 'month') && selectedDate && (
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
                  <Text style={styles.tradeLabel}>{strings.calendar.tradeLabel}</Text>
                  <Text style={[styles.tradeAmount, { color: t.amount >= 0 ? theme.colors.positive : theme.colors.negative }]}>
                    {t.amount >= 0 ? '+' : ''}
                    {formatMoney(t.amount)}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          )}
        </>
      )}

      <EditTradeModal
        visible={!!editingTrade}
        trade={editingTrade}
        onClose={() => setEditingTrade(null)}
        onSave={(updates) => updateTrade(challenge.id, editingTrade.id, updates)}
        onDelete={() => deleteTrade(challenge.id, editingTrade.id)}
      />
    </Screen>
  );
}

function WeekView({ strings, weekStart, dayTotalsMap, selectedDate, onSelectDate }) {
  const days = [];
  for (let i = 0; i < 7; i++) {
    const d = new Date(weekStart);
    d.setDate(d.getDate() + i);
    days.push(d);
  }

  return (
    <View style={styles.weekList}>
      {days.map((d) => {
        const key = `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
        const amount = dayTotalsMap.get(key);
        const hasData = amount !== undefined;
        const isSelected = selectedDate && isSameCalendarDay(selectedDate, d);
        return (
          <TouchableOpacity
            key={key}
            disabled={!hasData}
            onPress={() => onSelectDate(d)}
            style={[
              styles.weekRow,
              styles.weekRowDefault,
              hasData && (amount >= 0 ? styles.cellPositive : styles.cellNegative),
              isSelected && styles.cellSelected,
            ]}
          >
            <View style={styles.weekRowLeft}>
              <Text style={styles.weekRowDay}>{strings.calendar.weekdays[d.getDay()]}</Text>
              <Text style={styles.weekRowDate}>{shortDate(d)}</Text>
            </View>
            <Text
              style={[
                styles.weekRowAmount,
                { color: hasData ? (amount >= 0 ? theme.colors.positive : theme.colors.negative) : theme.colors.textMuted },
              ]}
            >
              {hasData ? `${amount >= 0 ? '+' : ''}${formatMoney(amount)}` : '—'}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

function MonthGrid({ strings, year, month, dayTotalsMap, selectedDate, onSelectDate }) {
  const firstWeekday = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const cells = [];
  for (let i = 0; i < firstWeekday; i++) cells.push(null);
  for (let day = 1; day <= daysInMonth; day++) cells.push(day);

  return (
    <>
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
              onPress={() => onSelectDate(cellDate)}
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
    </>
  );
}

function YearView({ strings, year, trades, onSelectMonth }) {
  return (
    <View style={styles.yearList}>
      {strings.calendar.months.map((label, m) => {
        const monthTrades = getMonthTrades(trades, year, m);
        const hasData = monthTrades.length > 0;
        const total = monthTrades.reduce((s, t) => s + t.amount, 0);
        return (
          <TouchableOpacity key={m} style={styles.yearRow} onPress={() => onSelectMonth(m)}>
            <Text style={styles.yearRowLabel}>{label}</Text>
            <Text
              style={[
                styles.yearRowAmount,
                { color: !hasData ? theme.colors.textMuted : total >= 0 ? theme.colors.positive : theme.colors.negative },
              ]}
            >
              {hasData ? `${total >= 0 ? '+' : ''}${formatMoney(total)}` : '—'}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

function AllTimeView({ strings, trades }) {
  if (trades.length === 0) {
    return <Text style={styles.emptyText}>{strings.calendar.emptyState}</Text>;
  }

  const days = groupTradesByDay(trades);
  const total = getTotalProfit(trades);
  const sorted = [...trades].sort((a, b) => new Date(a.date) - new Date(b.date));
  const bestDay = Math.max(...days.map((d) => d.amount));
  const worstDay = Math.min(...days.map((d) => d.amount));

  return (
    <View style={styles.panel}>
      <Text style={styles.panelTitle}>{strings.calendar.allTimeTitle}</Text>
      <View style={styles.allTimeHeadline}>
        <Text style={styles.headlineLabel}>{strings.calendar.allTimeNetPnl}</Text>
        <Text style={[styles.headlineValue, { color: total >= 0 ? theme.colors.positive : theme.colors.negative }]}>
          {total >= 0 ? '+' : ''}
          {formatMoney(total)}
        </Text>
      </View>
      <View style={styles.allTimeGrid}>
        <StatBox label={strings.calendar.allTimeTrades} value={String(trades.length)} />
        <StatBox label={strings.calendar.allTimeDays} value={String(days.length)} />
        <StatBox label={strings.calendar.allTimeBestDay} value={formatMoney(bestDay)} color={theme.colors.positive} />
        <StatBox label={strings.calendar.allTimeWorstDay} value={formatMoney(worstDay)} color={theme.colors.negative} />
      </View>
      <Text style={styles.allTimeRange}>
        {strings.calendar.allTimeRange
          .replace('{from}', formatDate(sorted[0].date))
          .replace('{to}', formatDate(sorted[sorted.length - 1].date))}
      </Text>
    </View>
  );
}

function StatBox({ label, value, color }) {
  return (
    <View style={styles.statBox}>
      <Text style={styles.statBoxLabel}>{label}</Text>
      <Text style={[styles.statBoxValue, color && { color }]}>{value}</Text>
    </View>
  );
}

const CELL_GAP = 3;

const styles = StyleSheet.create({
  accountList: { marginBottom: theme.spacing(3) },
  periodControl: { marginBottom: theme.spacing(3) },
  navRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: theme.spacing(3),
  },
  navLabel: {
    fontSize: 15,
    fontWeight: '700',
    color: theme.colors.textPrimary,
  },
  weekdayRow: {
    flexDirection: 'row',
    marginBottom: 4,
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
  weekList: { gap: 6 },
  weekRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderRadius: theme.radius.md,
    paddingVertical: 12,
    paddingHorizontal: 14,
  },
  weekRowDefault: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
  },
  weekRowLeft: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  weekRowDay: { fontSize: 12, fontWeight: '700', color: theme.colors.textSecondary, width: 16, textAlign: 'center' },
  weekRowDate: { fontSize: 13, color: theme.colors.textPrimary },
  weekRowAmount: { fontSize: 13.5, fontWeight: '700' },
  yearList: { gap: 6 },
  yearRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    paddingVertical: 12,
    paddingHorizontal: 14,
  },
  yearRowLabel: { fontSize: 13.5, color: theme.colors.textPrimary, fontWeight: '600' },
  yearRowAmount: { fontSize: 13.5, fontWeight: '700' },
  panel: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.lg,
    padding: theme.spacing(4),
  },
  panelTitle: { fontSize: 14, fontWeight: '700', color: theme.colors.textPrimary, marginBottom: theme.spacing(2) },
  allTimeHeadline: { alignItems: 'center', marginVertical: theme.spacing(3) },
  headlineLabel: { fontSize: 12, color: theme.colors.textSecondary, marginBottom: 4 },
  headlineValue: { fontSize: 28, fontWeight: '800' },
  allTimeGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  statBox: {
    flexBasis: '47%',
    flexGrow: 1,
    backgroundColor: theme.colors.background,
    borderRadius: theme.radius.md,
    padding: 12,
    alignItems: 'center',
  },
  statBoxLabel: { fontSize: 10.5, color: theme.colors.textSecondary, marginBottom: 4, textAlign: 'center' },
  statBoxValue: { fontSize: 15, fontWeight: '700', color: theme.colors.textPrimary },
  allTimeRange: {
    fontSize: 11,
    color: theme.colors.textMuted,
    textAlign: 'center',
    marginTop: theme.spacing(3),
  },
  divider: {
    height: 1,
    backgroundColor: theme.colors.border,
    marginVertical: theme.spacing(4),
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
  emptyText: { color: theme.colors.textSecondary, fontSize: 13, textAlign: 'center', marginTop: theme.spacing(3) },
});
