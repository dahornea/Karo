import { Clone, useGLTF } from '@react-three/drei';
import { Suspense } from 'react';
import type { ReactNode } from 'react';
import type { PieceType } from '../assets/game/gameAssets';
import { gameAssets } from '../assets/game/gameAssets';

interface OptionalModel3DProps {
  type: PieceType;
  fallback: ReactNode;
  scale?: number;
}

export function OptionalModel3D({ type, fallback, scale = 1 }: OptionalModel3DProps) {
  const asset = gameAssets.models3d[type];

  if (!asset) {
    return <>{fallback}</>;
  }

  return (
    <Suspense fallback={fallback}>
      <LoadedModel src={asset.src} scale={scale} />
    </Suspense>
  );
}

function LoadedModel({ src, scale }: { src: string; scale: number }) {
  const model = useGLTF(src);
  return <Clone castShadow object={model.scene} receiveShadow scale={scale} />;
}
