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
import PrimaryButton from '../components/PrimaryButton';
import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { useChallenges } from '../context/ChallengesContext';

const CONSISTENCY_MODE_OPTIONS = [
  { label: strings.onboarding.consistencyNone, value: 'none' },
  { label: strings.onboarding.consistencyWithLimit, value: 'limit' },
];

const DRAWDOWN_TYPE_OPTIONS = [
  { label: strings.onboarding.drawdownTypeStatic, value: 'static' },
  { label: strings.onboarding.drawdownTypeTrailing, value: 'trailing' },
];

export default function SettingsScreen() {
  const navigation = useNavigation();
  const { activeChallenge, challenges, setActiveChallenge, updateChallenge, deleteChallenge } = useChallenges();
  const [form, setForm] = useState(activeChallenge);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setForm(activeChallenge);
  }, [activeChallenge?.id]);

  if (!activeChallenge || !form) {
    return (
      <Screen>
        <ScreenHeader title={strings.tabs.settings} />
        <Text style={styles.emptyText}>No hay ninguna cuenta activa.</Text>
      </Screen>
    );
  }

  const updateField = (field) => (value) => setForm((f) => ({ ...f, [field]: value }));

  const consistencyMode = form.consistencyLimitPct === null ? 'none' : 'limit';
  const handleConsistencyModeChange = (mode) => {
    setForm((f) => ({ ...f, consistencyLimitPct: mode === 'none' ? null : f.consistencyLimitPct ?? 40 }));
  };

  const handleSave = () => {
    updateChallenge(activeChallenge.id, {
      ...form,
      accountSize: Number(form.accountSize) || 0,
      profitTarget: Number(form.profitTarget) || 0,
      maxDrawdown: Number(form.maxDrawdown) || 0,
      minProfitableDays: Number(form.minProfitableDays) || 0,
      consistencyLimitPct: form.consistencyLimitPct === null ? null : Number(form.consistencyLimitPct) || 0,
    });
    setSaved(true);
    setTimeout(() => setSaved(false), 1500);
  };

  const handleDelete = () => {
    Alert.alert(
      'Eliminar esta cuenta/challenge',
      `Se borrará "${activeChallenge.name}" y todo su historial de días. Esta acción no se puede deshacer.`,
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Eliminar',
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
      <ScreenHeader title={strings.tabs.settings} />

      <FieldLabel>Tus cuentas</FieldLabel>
      <View style={styles.accountList}>
        {challenges.map((c) => (
          <PresetCard
            key={c.id}
            label={c.name}
            active={c.id === activeChallenge.id}
            onPress={() => setActiveChallenge(c.id)}
          />
        ))}
        <TouchableOpacity style={styles.addAccountRow} onPress={() => navigation.navigate('Onboarding')}>
          <Ionicons name="add" size={18} color={theme.colors.accent} />
          <Text style={styles.addAccountLabel}>Agregar cuenta nueva</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.divider} />

      <ScreenHeader eyebrow="EDITAR PARÁMETROS" title={activeChallenge.name} />

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
        label={saved ? 'Guardado ✓' : 'Guardar cambios'}
        onPress={handleSave}
        style={styles.saveButton}
      />

      <Text style={styles.deleteButton} onPress={handleDelete}>
        Eliminar esta cuenta/challenge
      </Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  accountList: { marginBottom: theme.spacing(2) },
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
