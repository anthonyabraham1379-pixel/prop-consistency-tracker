import React from 'react';
import { Alert, Linking, StyleSheet, Switch, Text, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import { theme } from '../config/theme';
import { strings } from '../config/strings';
import { usePreferences } from '../context/PreferencesContext';

const SUPPORT_EMAIL = 'a.isis.a0412@gmail.com';
const APP_VERSION = '1.0.0';

function Row({ icon, label, onPress, right, disabled, last }) {
  return (
    <TouchableOpacity
      onPress={onPress}
      disabled={disabled || !onPress}
      style={[styles.row, last && styles.rowLast, disabled && styles.rowDisabled]}
      activeOpacity={onPress ? 0.7 : 1}
    >
      <View style={styles.rowLeft}>
        <Ionicons name={icon} size={18} color={theme.colors.textSecondary} />
        <Text style={[styles.rowLabel, disabled && styles.rowLabelDisabled]}>{label}</Text>
      </View>
      {right}
    </TouchableOpacity>
  );
}

function ComingSoonBadge() {
  return (
    <View style={styles.comingSoonBadge}>
      <Text style={styles.comingSoonText}>{strings.generalSettings.comingSoon}</Text>
    </View>
  );
}

export default function GeneralSettingsScreen() {
  const navigation = useNavigation();
  const { hidePnl, setHidePnl } = usePreferences();

  const showComingSoon = () => {
    Alert.alert(strings.generalSettings.comingSoon, strings.generalSettings.comingSoonBody);
  };

  const handleContact = () => {
    Linking.openURL(`mailto:${SUPPORT_EMAIL}`).catch(() => {});
  };

  const handleAbout = () => {
    Alert.alert(
      strings.generalSettings.aboutApp,
      strings.generalSettings.aboutBody.replace('{version}', APP_VERSION)
    );
  };

  return (
    <Screen>
      <ScreenHeader title={strings.generalSettings.title} onBack={() => navigation.goBack()} />

      <Text style={styles.sectionLabel}>{strings.generalSettings.generalSection}</Text>
      <View style={styles.card}>
        <Row
          icon="eye-off-outline"
          label={strings.generalSettings.hidePnl}
          last
          right={
            <Switch
              value={hidePnl}
              onValueChange={setHidePnl}
              trackColor={{ false: theme.colors.border, true: theme.colors.positive }}
              thumbColor="#fff"
            />
          }
        />
      </View>

      <Text style={styles.sectionLabel}>{strings.generalSettings.accountSection}</Text>
      <View style={styles.card}>
        <Row icon="logo-google" label={strings.generalSettings.googleLogin} onPress={showComingSoon} right={<ComingSoonBadge />} disabled />
        <Row icon="star-outline" label={strings.generalSettings.premium} onPress={showComingSoon} right={<ComingSoonBadge />} disabled last />
      </View>

      <Text style={styles.sectionLabel}>{strings.generalSettings.supportSection}</Text>
      <View style={styles.card}>
        <Row
          icon="mail-outline"
          label={strings.generalSettings.contactUs}
          onPress={handleContact}
          right={<Ionicons name="chevron-forward" size={18} color={theme.colors.textMuted} />}
        />
        <Row
          icon="information-circle-outline"
          label={strings.generalSettings.aboutApp}
          onPress={handleAbout}
          right={<Ionicons name="chevron-forward" size={18} color={theme.colors.textMuted} />}
          last
        />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  sectionLabel: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
    color: theme.colors.textMuted,
    marginBottom: 8,
    marginTop: theme.spacing(2),
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.lg,
    marginBottom: theme.spacing(5),
    overflow: 'hidden',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.border,
  },
  rowLast: { borderBottomWidth: 0 },
  rowDisabled: { opacity: 0.6 },
  rowLeft: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  rowLabel: { fontSize: 14, color: theme.colors.textPrimary },
  rowLabelDisabled: { color: theme.colors.textSecondary },
  comingSoonBadge: {
    backgroundColor: theme.colors.background,
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  comingSoonText: { fontSize: 10.5, fontWeight: '700', color: theme.colors.textMuted },
});
