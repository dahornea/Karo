import { useCallback, useState } from 'react';
import type { BoardRendererMode } from '../types/boardRenderer';

export function useBoardRendererMode() {
  const [mode, setModeState] = useState<BoardRendererMode>(() => readInitialBoardRendererMode());

  const setMode = useCallback((nextMode: BoardRendererMode) => {
    setModeState(nextMode);

    if (typeof window === 'undefined') {
      return;
    }

    const url = new URL(window.location.href);

    if (nextMode === '3d') {
      url.searchParams.set('board', '3d');
    } else {
      url.searchParams.delete('board');
    }

    window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
  }, []);

  return {
    boardRendererMode: mode,
    setBoardRendererMode: setMode
  };
}

function readInitialBoardRendererMode(): BoardRendererMode {
  if (typeof window === 'undefined') {
    return '2d';
  }

  const queryValue = new URLSearchParams(window.location.search).get('board')?.toLowerCase();
  return queryValue === '3d' ? '3d' : '2d';
}

