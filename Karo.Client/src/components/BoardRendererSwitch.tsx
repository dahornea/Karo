import { Component, Suspense, lazy } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import type { BoardRendererMode } from '../types/boardRenderer';
import { GameBoard } from './GameBoard';
import type { GameBoardProps } from './GameBoard';

const LazyBoard3DRenderer = lazy(() =>
  import('./Board3DRenderer').then((module) => ({
    default: module.Board3DRenderer
  }))
);

interface BoardRendererSwitchProps extends GameBoardProps {
  rendererMode: BoardRendererMode;
  onRendererModeChange: (mode: BoardRendererMode) => void;
}

export function BoardRendererSwitch({
  rendererMode,
  onRendererModeChange,
  ...boardProps
}: BoardRendererSwitchProps) {
  const toolbarAction = (
    <BoardRendererToggle mode={rendererMode} onModeChange={onRendererModeChange} />
  );

  return (
    <div className="board-renderer-shell" data-renderer={rendererMode}>
      {rendererMode === '3d' ? (
        <BoardRendererErrorBoundary
          fallback={<Board2DRenderer {...boardProps} toolbarAction={toolbarAction} />}
          onRendererError={() => onRendererModeChange('2d')}
          resetKey={rendererMode}
        >
          <Suspense fallback={<Board2DRenderer {...boardProps} toolbarAction={toolbarAction} />}>
            <LazyBoard3DRenderer {...boardProps} toolbarAction={toolbarAction} />
          </Suspense>
        </BoardRendererErrorBoundary>
      ) : (
        <Board2DRenderer {...boardProps} toolbarAction={toolbarAction} />
      )}
    </div>
  );
}

function Board2DRenderer(props: GameBoardProps) {
  return <GameBoard {...props} />;
}

function BoardRendererToggle({
  mode,
  onModeChange
}: {
  mode: BoardRendererMode;
  onModeChange: (mode: BoardRendererMode) => void;
}) {
  return (
    <div className="board-renderer-toggle" aria-label="Board renderer">
      <button
        aria-pressed={mode === '2d'}
        type="button"
        onClick={() => onModeChange('2d')}
      >
        2D
      </button>
      <button
        aria-pressed={mode === '3d'}
        aria-label="3D experimental renderer"
        title="Experimental renderer"
        type="button"
        onClick={() => onModeChange('3d')}
      >
        3D Exp.
      </button>
    </div>
  );
}

class BoardRendererErrorBoundary extends Component<{
  children: ReactNode;
  fallback: ReactNode;
  onRendererError: () => void;
  resetKey: BoardRendererMode;
}, {
  hasError: boolean;
}> {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.warn('Karo 3D renderer failed; falling back to the supported 2D board.', error, info);
    this.props.onRendererError();
  }

  componentDidUpdate(previousProps: { resetKey: BoardRendererMode }) {
    if (previousProps.resetKey !== this.props.resetKey && this.state.hasError) {
      this.setState({ hasError: false });
    }
  }

  render() {
    return this.state.hasError ? this.props.fallback : this.props.children;
  }
}
