import React, { useState } from 'react';
import { StyleSheet, Text, TextInput, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import FieldLabel from '../components/FieldLabel';
import NumField from '../components/NumField';
import SegmentedControl from '../components/SegmentedControl';
import PresetCard from '../components/PresetCard';
import PrimaryButton from '../components/PrimaryButton';
import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { EMPTY_CHALLENGE, FIRM_PRESETS } from '../config/defaultParams';
import { useChallenges } from '../context/ChallengesContext';

const CONSISTENCY_MODE_OPTIONS = [
  { label: strings.onboarding.consistencyNone, value: 'none' },
  { label: strings.onboarding.consistencyWithLimit, value: 'limit' },
];

const DRAWDOWN_TYPE_OPTIONS = [
  { label: strings.onboarding.drawdownTypeStatic, value: 'static' },
  { label: strings.onboarding.drawdownTypeTrailing, value: 'trailing' },
];

export default function OnboardingScreen() {
  const navigation = useNavigation();
  const { addChallenge } = useChallenges();
  const [form, setForm] = useState(EMPTY_CHALLENGE);
  const [presetIndex, setPresetIndex] = useState(null);

  const updateField = (field) => (value) => setForm((f) => ({ ...f, [field]: value }));

  const consistencyMode = form.consistencyLimitPct === null ? 'none' : 'limit';
  const handleConsistencyModeChange = (mode) => {
    setForm((f) => ({ ...f, consistencyLimitPct: mode === 'none' ? null : f.consistencyLimitPct ?? 40 }));
  };

  const applyPreset = (index) => {
    setPresetIndex(index);
    const preset = FIRM_PRESETS[index];
    const isCustom = preset.label === strings.onboarding.presetCustomLabel;
    setForm((f) => ({
      ...f,
      name: isCustom ? '' : preset.label,
      accountSize: preset.accountSize ?? f.accountSize,
      profitTarget: preset.profitTarget ?? f.profitTarget,
      maxDrawdown: preset.maxDrawdown ?? f.maxDrawdown,
      drawdownType: preset.drawdownType,
      consistencyLimitPct: preset.consistencyLimitPct,
      minProfitableDays: preset.minProfitableDays,
    }));
  };

  const canSubmit = form.name.trim().length > 0;

  const handleSubmit = () => {
    if (!canSubmit) return;
    addChallenge({
      ...form,
      accountSize: Number(form.accountSize) || 0,
      profitTarget: Number(form.profitTarget) || 0,
      maxDrawdown: Number(form.maxDrawdown) || 0,
      minProfitableDays: Number(form.minProfitableDays) || 0,
      consistencyLimitPct: form.consistencyLimitPct === null ? null : Number(form.consistencyLimitPct) || 0,
    });
    navigation.reset({ index: 0, routes: [{ name: 'Main' }] });
  };

  return (
    <Screen>
      <ScreenHeader
        eyebrow={strings.onboarding.eyebrow}
        title={strings.onboarding.title}
        subtitle={strings.onboarding.subtitle}
        onBack={navigation.canGoBack() ? navigation.goBack : undefined}
      />

      <FieldLabel>{strings.onboarding.presetLabel}</FieldLabel>
      <View style={styles.presetList}>
        {FIRM_PRESETS.map((preset, index) => (
          <PresetCard
            key={preset.label}
            label={preset.label}
            active={presetIndex === index}
            onPress={() => applyPreset(index)}
          />
        ))}
      </View>
      <Text style={styles.presetDisclaimer}>
        Verifica siempre las reglas actuales de tu firm — estos valores son solo un punto de partida.
      </Text>

      <FieldLabel>{strings.onboarding.nameLabel}</FieldLabel>
      <TextInput
        style={styles.input}
        value={form.name}
        onChangeText={updateField('name')}
        placeholder={strings.onboarding.namePlaceholder}
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
        label={strings.onboarding.submit}
        onPress={handleSubmit}
        disabled={!canSubmit}
        style={styles.submitButton}
      />
      <Text style={styles.hint}>{strings.onboarding.hint}</Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  presetList: { marginBottom: 8 },
  presetDisclaimer: {
    fontSize: 11.5,
    color: theme.colors.textMuted,
    marginBottom: theme.spacing(5),
    lineHeight: 16,
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
  submitButton: { marginTop: theme.spacing(6) },
  hint: {
    fontSize: 11.5,
    color: theme.colors.textMuted,
    textAlign: 'center',
    marginTop: 10,
    lineHeight: 16,
  },
});
