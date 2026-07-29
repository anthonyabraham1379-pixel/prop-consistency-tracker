import React from 'react';
import { ActivityIndicator, Alert, Linking, StyleSheet, Switch, Text, TouchableOpacity, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';

import Screen from '../components/Screen';
import ScreenHeader from '../components/ScreenHeader';
import SegmentedControl from '../components/SegmentedControl';
import { theme } from '../config/theme';
import { useStrings } from '../config/strings';
import { usePreferences } from '../context/PreferencesContext';
import { useAuth } from '../context/AuthContext';
import { usePremium } from '../context/PremiumContext';

const SUPPORT_EMAIL = 'a.isis.a0412@gmail.com';
const APP_VERSION = '1.0.0';

function Row({ icon, label, onPress, right, disabled, last, danger }) {
  return (
    <TouchableOpacity
      onPress={onPress}
      disabled={disabled || !onPress}
      style={[styles.row, last && styles.rowLast, disabled && styles.rowDisabled]}
      activeOpacity={onPress ? 0.7 : 1}
    >
      <View style={styles.rowLeft}>
        <Ionicons name={icon} size={18} color={danger ? theme.colors.negative : theme.colors.textSecondary} />
        <Text style={[styles.rowLabel, disabled && styles.rowLabelDisabled, danger && styles.rowLabelDanger]}>
          {label}
        </Text>
      </View>
      {right}
    </TouchableOpacity>
  );
}

function ComingSoonBadge({ label }) {
  return (
    <View style={styles.comingSoonBadge}>
      <Text style={styles.comingSoonText}>{label}</Text>
    </View>
  );
}

export default function GeneralSettingsScreen() {
  const navigation = useNavigation();
  const strings = useStrings();
  const { hidePnl, setHidePnl, language, setLanguage } = usePreferences();
  const { user, signingIn, signInWithGoogle, signOut } = useAuth();
  const { isPremium, restorePurchases } = usePremium();
  const [restoring, setRestoring] = React.useState(false);

  const languageOptions = [
    { label: strings.generalSettings.languageSpanish, value: 'es' },
    { label: strings.generalSettings.languageEnglish, value: 'en' },
  ];

  const showComingSoon = () => {
    Alert.alert(strings.generalSettings.comingSoon, strings.generalSettings.comingSoonBody);
  };

  const handleGoogleAuth = async () => {
    if (user) return;
    try {
      await signInWithGoogle();
    } catch (e) {
      Alert.alert(strings.generalSettings.googleLogin, e.message || strings.generalSettings.signInError);
    }
  };

  const handleSignOutPress = () => {
    Alert.alert(strings.generalSettings.signOutConfirmTitle, strings.generalSettings.signOutConfirmBody, [
      { text: strings.common.cancel, style: 'cancel' },
      {
        text: strings.generalSettings.signOut,
        style: 'destructive',
        onPress: async () => {
          try {
            await signOut();
          } catch (e) {
            Alert.alert(strings.generalSettings.googleLogin, e.message || strings.generalSettings.signInError);
          }
        },
      },
    ]);
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

  const handleRestorePurchases = async () => {
    setRestoring(true);
    try {
      const info = await restorePurchases();
      const hasActive = !!info?.entitlements?.active && Object.keys(info.entitlements.active).length > 0;
      Alert.alert(
        strings.premium.title,
        hasActive ? strings.generalSettings.restoreSuccess : strings.generalSettings.restoreEmpty
      );
    } catch (e) {
      Alert.alert(strings.premium.title, e.message || strings.generalSettings.restoreError);
    } finally {
      setRestoring(false);
    }
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

      <Text style={styles.sectionLabel}>{strings.generalSettings.languageSection}</Text>
      <View style={styles.card}>
        <View style={styles.languageRow}>
          <SegmentedControl options={languageOptions} value={language} onChange={setLanguage} style={styles.languageControl} />
        </View>
      </View>

      <Text style={styles.sectionLabel}>{strings.generalSettings.accountSection}</Text>
      <View style={styles.card}>
        <Row
          icon="logo-google"
          label={user ? user.email : strings.generalSettings.googleLogin}
          onPress={user ? undefined : handleGoogleAuth}
          last={!user}
          right={
            signingIn ? (
              <ActivityIndicator color={theme.colors.accent} />
            ) : user ? (
              <Ionicons name="checkmark-circle" size={18} color={theme.colors.positive} />
            ) : (
              <Ionicons name="chevron-forward" size={18} color={theme.colors.textMuted} />
            )
          }
        />
        {user && (
          <View style={styles.syncNote}>
            <Ionicons name="cloud-done-outline" size={14} color={theme.colors.textMuted} />
            <Text style={styles.syncNoteText}>{strings.generalSettings.syncBody}</Text>
          </View>
        )}
        <Row
          icon="star-outline"
          label={isPremium ? strings.generalSettings.premiumActive : strings.generalSettings.premium}
          onPress={isPremium ? undefined : () => navigation.navigate('Paywall')}
          right={
            isPremium ? undefined : <Ionicons name="chevron-forward" size={18} color={theme.colors.textMuted} />
          }
        />
        <Row
          icon="document-text-outline"
          label={strings.generalSettings.exportFeature}
          onPress={showComingSoon}
          right={<ComingSoonBadge label={strings.generalSettings.comingSoon} />}
          disabled
        />
        <Row
          icon="camera-outline"
          label={strings.generalSettings.screenshotsFeature}
          onPress={showComingSoon}
          right={<ComingSoonBadge label={strings.generalSettings.comingSoon} />}
          disabled
        />
        <Row
          icon="refresh-outline"
          label={strings.generalSettings.restorePurchases}
          onPress={handleRestorePurchases}
          right={restoring ? <ActivityIndicator color={theme.colors.accent} /> : undefined}
          last
        />
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
          last={!user}
        />
        {user && (
          <Row
            icon="log-out-outline"
            label={strings.generalSettings.signOut}
            onPress={handleSignOutPress}
            last
            danger
          />
        )}
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
  syncNote: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 16,
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: theme.colors.border,
  },
  syncNoteText: { fontSize: 11, color: theme.colors.textMuted, flexShrink: 1 },
  languageRow: { paddingHorizontal: 16, paddingVertical: 14 },
  languageControl: { marginBottom: 0 },
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
  rowLabelDanger: { color: theme.colors.negative, fontWeight: '600' },
  comingSoonBadge: {
    backgroundColor: theme.colors.background,
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  comingSoonText: { fontSize: 10.5, fontWeight: '700', color: theme.colors.textMuted },
});
