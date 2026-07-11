import React, { useEffect, useState } from 'react';
import { Alert, Modal, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { theme } from '../config/theme';
import IconButton from './IconButton';
import FieldLabel from './FieldLabel';
import NumField from './NumField';
import SegmentedControl from './SegmentedControl';
import DateTimeField from './DateTimeField';
import PrimaryButton from './PrimaryButton';

const SIGN_OPTIONS = [
  { label: 'Ganancia', value: 'gain' },
  { label: 'Pérdida', value: 'loss' },
];

export default function EditTradeModal({ visible, trade, onClose, onSave, onDelete }) {
  const insets = useSafeAreaInsets();
  const [sign, setSign] = useState('gain');
  const [amount, setAmount] = useState('');
  const [date, setDate] = useState(new Date());

  useEffect(() => {
    if (visible && trade) {
      setSign(trade.amount < 0 ? 'loss' : 'gain');
      setAmount(String(Math.abs(trade.amount)));
      setDate(new Date(trade.date));
    }
  }, [visible, trade]);

  const handleSave = () => {
    const magnitude = Number(amount) || 0;
    if (magnitude === 0) return;
    onSave({ amount: sign === 'loss' ? -magnitude : magnitude, date: date.toISOString() });
    onClose();
  };

  const handleDelete = () => {
    Alert.alert('Eliminar trade', 'Esta acción no se puede deshacer.', [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Eliminar',
        style: 'destructive',
        onPress: () => {
          onDelete();
          onClose();
        },
      },
    ]);
  };

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={[styles.sheet, { paddingBottom: theme.spacing(4) + insets.bottom }]}>
          <View style={styles.header}>
            <Text style={styles.title}>Editar trade</Text>
            <IconButton name="close" onPress={onClose} />
          </View>

          <FieldLabel>Resultado del día</FieldLabel>
          <SegmentedControl options={SIGN_OPTIONS} value={sign} onChange={setSign} />

          <NumField label="Monto" value={amount} onChange={setAmount} prefix="$" />

          <DateTimeField label="Fecha y hora" value={date} onChange={setDate} />

          <PrimaryButton label="Guardar cambios" onPress={handleSave} style={styles.saveButton} disabled={!amount} />
          <Text style={styles.deleteLink} onPress={handleDelete}>
            Eliminar este trade
          </Text>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.55)',
    justifyContent: 'flex-end',
  },
  sheet: {
    backgroundColor: theme.colors.background,
    borderTopLeftRadius: theme.radius.lg,
    borderTopRightRadius: theme.radius.lg,
    borderWidth: 1,
    borderColor: theme.colors.border,
    padding: theme.spacing(4),
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: theme.spacing(4),
  },
  title: {
    fontSize: 16,
    fontWeight: '700',
    color: theme.colors.textPrimary,
  },
  saveButton: { marginTop: theme.spacing(2) },
  deleteLink: {
    textAlign: 'center',
    color: theme.colors.negative,
    fontSize: 13,
    fontWeight: '600',
    marginTop: theme.spacing(4),
    paddingVertical: 10,
  },
});
