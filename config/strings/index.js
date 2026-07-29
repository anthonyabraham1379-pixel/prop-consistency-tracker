import { es } from './es';
import { en } from './en';
import { usePreferences } from '../../context/PreferencesContext';
import { DEFAULT_LANGUAGE } from '../language';

export const STRINGS = { es, en };
export { DEFAULT_LANGUAGE };

export function useStrings() {
  const { language } = usePreferences();
  return STRINGS[language] || STRINGS[DEFAULT_LANGUAGE];
}
