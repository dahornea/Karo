import { useCallback, useMemo, useState } from 'react';

const debugStorageKey = 'karoDebugMode';

export function useDebugMode() {
  const isAvailable = import.meta.env.DEV;
  const initialEnabled = useMemo(() => {
    if (!isAvailable || typeof window === 'undefined') {
      return false;
    }

    const queryValue = new URLSearchParams(window.location.search).get('debug');
    if (queryValue === 'true') {
      window.localStorage.setItem(debugStorageKey, 'true');
      return true;
    }

    if (queryValue === 'false') {
      window.localStorage.removeItem(debugStorageKey);
      return false;
    }

    return window.localStorage.getItem(debugStorageKey) === 'true';
  }, [isAvailable]);
  const [isEnabled, setIsEnabledState] = useState(initialEnabled);

  const setIsEnabled = useCallback(
    (enabled: boolean) => {
      if (!isAvailable || typeof window === 'undefined') {
        return;
      }

      setIsEnabledState(enabled);
      if (enabled) {
        window.localStorage.setItem(debugStorageKey, 'true');
      } else {
        window.localStorage.removeItem(debugStorageKey);
        const url = new URL(window.location.href);
        if (url.searchParams.has('debug')) {
          url.searchParams.delete('debug');
          window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
        }
      }
    },
    [isAvailable]
  );

  return {
    isDebugAvailable: isAvailable,
    isDebugEnabled: isAvailable && isEnabled,
    setIsDebugEnabled: setIsEnabled
  };
}
