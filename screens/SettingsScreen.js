import React, { useEffect, useState } from 'react';
import { Alert, StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import FieldLabel from '../components/FieldLabel';
import NumField from '../components/NumField';
import SegmentedControl from '../components/SegmentedControl';
import PresetCard from '../components/PresetCard';
import IconButton from '../components/IconButton';
import PrimaryButton from '../components/PrimaryButton';
import { theme } from '../config/theme';
import { useStrings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';
import { usePremium } from '../context/PremiumContext';
import { getTotalProfit } from '../utils/calculations';
import { formatDate, formatMoney } from '../utils/format';

export default function SettingsScreen() {
  const navigation = useNavigation();
  const strings = useStrings();
  const { activeChallenge, challenges, setActiveChallenge, updateChallenge, deleteChallenge, upgradeToFunded } =
    useChallenges();
  const { isPremium } = usePremium();
  const [form, setForm] = useState(activeChallenge);
  const [saved, setSaved] = useState(false);

  const CONSISTENCY_MODE_OPTIONS = [
    { label: strings.onboarding.consistencyNone, value: 'none' },
    { label: strings.onboarding.consistencyWithLimit, value: 'limit' },
  ];

  const ACCOUNT_STATUS_OPTIONS = [
    { label: strings.accountStatus.evaluation, value: 'evaluation' },
    { label: strings.accountStatus.funded, value: 'funded' },
  ];

  const DRAWDOWN_TYPE_OPTIONS = [
    { label: strings.onboarding.drawdownTypeStatic, value: 'static' },
    { label: strings.onboarding.drawdownTypeTrailing, value: 'trailing' },
  ];

  useEffect(() => {
    setForm(activeChallenge);
  }, [activeChallenge?.id]);

  if (!activeChallenge || !form) {
    return (
      <Screen>
        <ScreenHeader title={strings.tabs.settings} />
        <Text style={styles.emptyText}>{strings.common.noActiveAccount}</Text>
      </Screen>
    );
  }

  const updateField = (field) => (value) => setForm((f) => ({ ...f, [field]: value }));

  const consistencyMode = form.consistencyLimitPct === null ? 'none' : 'limit';
  const handleConsistencyModeChange = (mode) => {
    setForm((f) => ({ ...f, consistencyLimitPct: mode === 'none' ? null : f.consistencyLimitPct ?? 40 }));
  };

  const handleSave = () => {
    // No se incluyen status/evaluationArchive/trades aquí — esos los maneja
    // exclusivamente upgradeToFunded, para no pisarlos con datos viejos del form.
    updateChallenge(activeChallenge.id, {
      name: form.name,
      accountSize: Number(form.accountSize) || 0,
      profitTarget: Number(form.profitTarget) || 0,
      maxDrawdown: Number(form.maxDrawdown) || 0,
      minProfitableDays: Number(form.minProfitableDays) || 0,
      drawdownType: form.drawdownType,
      consistencyLimitPct: form.consistencyLimitPct === null ? null : Number(form.consistencyLimitPct) || 0,
    });
    setSaved(true);
    setTimeout(() => setSaved(false), 1500);
  };

  const handleStatusChange = (value) => {
    if (value !== 'funded' || activeChallenge.status === 'funded') return;
    Alert.alert(strings.accountStatus.confirmTitle, strings.accountStatus.confirmBody, [
      { text: strings.common.cancel, style: 'cancel' },
      { text: strings.accountStatus.confirmAction, onPress: () => upgradeToFunded(activeChallenge.id) },
    ]);
  };

  const handleDelete = () => {
    Alert.alert(
      strings.settings.deleteConfirmTitle,
      strings.settings.deleteConfirmBody.replace('{name}', activeChallenge.name),
      [
        { text: strings.common.cancel, style: 'cancel' },
        {
          text: strings.settings.deleteConfirmAction,
          style: 'destructive',
          onPress: () => {
            const wasLast = challenges.length <= 1;
            deleteChallenge(activeChallenge.id);
            if (wasLast) {
              navigation.reset({ index: 0, routes: [{ name: 'Onboarding' }] });
            }
          },
        },
      ]
    );
  };

  return (
    <Screen>
      <ScreenHeader
        title={strings.tabs.settings}
        right={<IconButton name="settings-outline" onPress={() => navigation.navigate('GeneralSettings')} />}
      />

      <FieldLabel>{strings.settings.yourAccounts}</FieldLabel>
      <View style={styles.accountList}>
        {challenges.map((c) => (
          <PresetCard
            key={c.id}
            label={c.name}
            active={c.id === activeChallenge.id}
            onPress={() => setActiveChallenge(c.id)}
          />
        ))}
        <TouchableOpacity
          style={styles.addAccountRow}
          onPress={() => {
            if (!isPremium && challenges.length >= 1) {
              Alert.alert(strings.premium.accountLimitTitle, strings.premium.accountLimitBody, [
                { text: strings.common.cancel, style: 'cancel' },
                { text: strings.premium.title, onPress: () => navigation.navigate('Paywall') },
              ]);
              return;
            }
            navigation.navigate('Onboarding');
          }}
        >
          <Ionicons
            name={!isPremium && challenges.length >= 1 ? 'lock-closed' : 'add'}
            size={18}
            color={theme.colors.accent}
          />
          <Text style={styles.addAccountLabel}>{strings.settings.addAccount}</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.divider} />

      <ScreenHeader eyebrow={strings.settings.editParamsEyebrow} title={activeChallenge.name} />

      <FieldLabel>{strings.accountStatus.label}</FieldLabel>
      {activeChallenge.status === 'funded' ? (
        <View style={styles.statusBadge}>
          <Ionicons name="checkmark-circle" size={16} color={theme.colors.positive} />
          <Text style={styles.statusBadgeText}>{strings.accountStatus.funded}</Text>
        </View>
      ) : (
        <SegmentedControl
          options={ACCOUNT_STATUS_OPTIONS}
          value={activeChallenge.status}
          onChange={handleStatusChange}
          style={styles.statusControl}
        />
      )}

      {activeChallenge.archivedPhases.length > 0 && (
        <View style={styles.archiveSection}>
          <FieldLabel>{strings.accountStatus.archivesLabel}</FieldLabel>
          {activeChallenge.archivedPhases.map((phase, index) => (
            <View key={index} style={styles.archiveCard}>
              <Text style={styles.archiveTitle}>
                {phase.outcome === 'funded'
                  ? strings.accountStatus.archiveTitleFunded
                  : strings.accountStatus.archiveTitleFailed}
              </Text>
              <Text style={styles.archiveBody}>
                {strings.accountStatus.archiveBody
                  .replace('{date}', formatDate(phase.archivedAt))
                  .replace('{profit}', formatMoney(getTotalProfit(phase.trades)))
                  .replace('{count}', String(phase.trades.length))}
              </Text>
            </View>
          ))}
        </View>
      )}

      <FieldLabel>{strings.onboarding.nameLabel}</FieldLabel>
      <TextInput
        style={styles.input}
        value={form.name}
        onChangeText={updateField('name')}
        placeholderTextColor={theme.colors.textMuted}
      />

      <View style={styles.row}>
        <NumField
          label={strings.onboarding.accountSizeLabel}
          value={form.accountSize}
          onChange={updateField('accountSize')}
          prefix="$"
          style={styles.rowItem}
        />
        <NumField
          label={strings.onboarding.profitTargetLabel}
          value={form.profitTarget}
          onChange={updateField('profitTarget')}
          prefix="$"
          style={styles.rowItem}
        />
      </View>
      <View style={styles.row}>
        <NumField
          label={strings.onboarding.maxDrawdownLabel}
          value={form.maxDrawdown}
          onChange={updateField('maxDrawdown')}
          prefix="$"
          style={styles.rowItem}
        />
        <NumField
          label={strings.onboarding.minProfitableDaysLabel}
          value={form.minProfitableDays}
          onChange={updateField('minProfitableDays')}
          style={styles.rowItem}
        />
      </View>

      <FieldLabel>{strings.onboarding.drawdownTypeLabel}</FieldLabel>
      <SegmentedControl
        options={DRAWDOWN_TYPE_OPTIONS}
        value={form.drawdownType}
        onChange={updateField('drawdownType')}
      />

      <FieldLabel style={styles.consistencyLabel}>{strings.onboarding.consistencyLabel}</FieldLabel>
      <SegmentedControl
        options={CONSISTENCY_MODE_OPTIONS}
        value={consistencyMode}
        onChange={handleConsistencyModeChange}
      />
      {consistencyMode === 'limit' && (
        <NumField
          label={strings.onboarding.consistencyPctLabel}
          value={form.consistencyLimitPct}
          onChange={updateField('consistencyLimitPct')}
          suffix="%"
        />
      )}

      <PrimaryButton
        label={saved ? strings.settings.saved : strings.settings.saveChanges}
        onPress={handleSave}
        style={styles.saveButton}
      />

      <Text style={styles.deleteButton} onPress={handleDelete}>
        {strings.settings.deleteLink}
      </Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  accountList: { marginBottom: theme.spacing(2) },
  statusControl: { marginBottom: theme.spacing(4) },
  statusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    alignSelf: 'flex-start',
    backgroundColor: 'rgba(74,222,128,0.1)',
    borderWidth: 1,
    borderColor: 'rgba(74,222,128,0.3)',
    borderRadius: theme.radius.md,
    paddingVertical: 8,
    paddingHorizontal: 14,
    marginBottom: theme.spacing(4),
  },
  statusBadgeText: { color: theme.colors.positive, fontWeight: '700', fontSize: 13 },
  archiveSection: { marginBottom: theme.spacing(2) },
  archiveCard: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    padding: theme.spacing(3),
    marginBottom: theme.spacing(4),
  },
  archiveTitle: { fontSize: 13, fontWeight: '700', color: theme.colors.textPrimary, marginBottom: 4 },
  archiveBody: { fontSize: 12, color: theme.colors.textSecondary, lineHeight: 17 },
  addAccountRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingVertical: 12,
    paddingHorizontal: 14,
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: theme.colors.accent,
  },
  addAccountLabel: {
    fontSize: 13.5,
    fontWeight: '600',
    color: theme.colors.accent,
  },
  divider: {
    height: 1,
    backgroundColor: theme.colors.border,
    marginVertical: theme.spacing(5),
  },
  input: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    paddingHorizontal: 13,
    paddingVertical: 11,
    color: theme.colors.textPrimary,
    fontSize: 14,
    marginBottom: theme.spacing(3),
  },
  row: { flexDirection: 'row', gap: 10 },
  rowItem: { flex: 1 },
  consistencyLabel: { marginTop: 14 },
  saveButton: { marginTop: theme.spacing(6) },
  deleteButton: {
    textAlign: 'center',
    color: theme.colors.negative,
    fontSize: 13,
    fontWeight: '600',
    marginTop: theme.spacing(4),
    paddingVertical: 10,
  },
  emptyText: { color: theme.colors.textSecondary, fontSize: 13 },
});
