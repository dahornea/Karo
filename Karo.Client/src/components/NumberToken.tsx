interface NumberTokenProps {
  x: number;
  y: number;
  value: number;
}

export function NumberToken({ x, y, value }: NumberTokenProps) {
  return (
    <g className="number-token" transform={`translate(${x} ${y + 14})`}>
      <circle r="17" />
      <text y="-2">{value}</text>
      <g className="probability-dots" transform="translate(0 8)">
        {getProbabilityDots(value).map((offset) => (
          <circle cx={offset} cy="0" r="1.8" key={offset} />
        ))}
      </g>
    </g>
  );
}

function getProbabilityDots(value: number) {
  const dotCount = Math.max(1, 6 - Math.abs(7 - value));
  const spacing = 5;
  const start = -((dotCount - 1) * spacing) / 2;

  return Array.from({ length: dotCount }, (_, index) => start + index * spacing);
}
