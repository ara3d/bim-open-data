// Counts the pixels of a just-rendered WebGL canvas that differ from its first pixel (the background).
export function countInkedPixels(canvas: HTMLCanvasElement): number {
  const copy = document.createElement('canvas');
  copy.width = canvas.width;
  copy.height = canvas.height;
  const context = copy.getContext('2d', { willReadFrequently: true });
  if (!context) return 0;
  context.drawImage(canvas, 0, 0);
  const { data } = context.getImageData(0, 0, copy.width, copy.height);
  let inked = 0;
  for (let i = 4; i < data.length; i += 4)
    if (data[i] !== data[0] || data[i + 1] !== data[1] || data[i + 2] !== data[2]) inked++;
  return inked;
}
