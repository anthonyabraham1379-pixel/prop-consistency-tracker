import React, { useState } from 'react';
import { Platform, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import DateTimePicker from '@react-native-community/datetimepicker';

import { theme } from '../config/theme';
import FieldLabel from './FieldLabel';

function formatDateLabel(d) {
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}/${d.getFullYear()}`;
}

function formatTimeLabel(d) {
  const hh = String(d.getHours()).padStart(2, '0');
  const mi = String(d.getMinutes()).padStart(2, '0');
  return `${hh}:${mi}`;
}

export default function DateTimeField({ label, value, onChange }) {
  const [showDate, setShowDate] = useState(false);
  const [showTime, setShowTime] = useState(false);

  const handleDateChange = (event, selected) => {
    setShowDate(Platform.OS === 'ios');
    if (event.type === 'dismissed' || !selected) return;
    const merged = new Date(value);
    merged.setFullYear(selected.getFullYear(), selected.getMonth(), selected.getDate());
    onChange(merged);
  };

  const handleTimeChange = (event, selected) => {
    setShowTime(Platform.OS === 'ios');
    if (event.type === 'dismissed' || !selected) return;
    const merged = new Date(value);
    merged.setHours(selected.getHours(), selected.getMinutes(), 0, 0);
    onChange(merged);
  };

  return (
    <View style={styles.wrap}>
      <FieldLabel>{label}</FieldLabel>
      <View style={styles.row}>
        <TouchableOpacity style={styles.box} onPress={() => setShowDate(true)}>
          <Ionicons name="calendar-outline" size={15} color={theme.colors.textMuted} />
          <Text style={styles.boxText}>{formatDateLabel(value)}</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.box} onPress={() => setShowTime(true)}>
          <Ionicons name="time-outline" size={15} color={theme.colors.textMuted} />
          <Text style={styles.boxText}>{formatTimeLabel(value)}</Text>
        </TouchableOpacity>
      </View>

      {showDate && (
        <DateTimePicker value={value} mode="date" display="default" maximumDate={new Date()} onChange={handleDateChange} />
      )}
      {showTime && (
        <DateTimePicker value={value} mode="time" display="default" onChange={handleTimeChange} />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginBottom: theme.spacing(3) },
  row: { flexDirection: 'row', gap: 10 },
  box: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: theme.colors.surface,
    borderWidth: 1,
    borderColor: theme.colors.border,
    borderRadius: theme.radius.md,
    paddingHorizontal: 12,
    paddingVertical: 11,
  },
  boxText: {
    color: theme.colors.textPrimary,
    fontSize: 13.5,
  },
});
