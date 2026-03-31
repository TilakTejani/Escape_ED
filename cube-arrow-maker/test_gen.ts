import { autoGenerateLevel } from './lib/generator';
import { Difficulty, GridSize } from './types';

const gridSize: GridSize = { x: 3, y: 3, z: 3 };
const level = autoGenerateLevel(gridSize, 4, 'medium');
console.log('Generated arrows:', level.length);
if (level.length > 0) {
  console.log('First arrow path:', level[0].path);
  const totalCovered = level.reduce((acc, a) => acc + a.path.length, 0);
  console.log('Total vertices covered:', totalCovered);
}
