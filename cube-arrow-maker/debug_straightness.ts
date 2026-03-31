
import { autoGenerateLevel } from './lib/generator';
import { Difficulty, GridSize } from './types';

const gridSize: GridSize = { x: 6, y: 6, z: 6 };
const maxPathLen = 4;
const difficulty: Difficulty = 'medium';

console.log('--- TESTING LOW BIAS (0.0) ---');
const arrowsLow = autoGenerateLevel(gridSize, maxPathLen, difficulty, 0.0);
console.log('Total Arrows:', arrowsLow.length);
// Count edge heads logic (simplified for debug)
// In reality we need geometry but we can just check if any arrow is length 2 and ends on boundary.

console.log('\n--- TESTING HIGH BIAS (1.0) ---');
const arrowsHigh = autoGenerateLevel(gridSize, maxPathLen, difficulty, 1.0);
console.log('Total Arrows:', arrowsHigh.length);

function analyze(arrows: any[], gridSize: any) {
    if (arrows.length === 0) return;
    const lens = arrows.map(a => a.path.length);
    const avgLen = lens.reduce((a, b) => a + b, 0) / lens.length;
    
    // Count heads on edges (simplified boundary check for debug)
    let edgeHeads = 0;
    // Assuming 6x6x6 grid, coords are in path. Need to simulate geometry.gridCoords
    // But since we don't have geometry here, let's just log arrows.
    console.log('Total Arrows:', arrows.length);
    console.log('Avg Path Length:', avgLen.toFixed(2));
}

console.log('\nLow Bias Analysis (Expected: Few edge heads):');
analyze(arrowsLow, gridSize);

console.log('\nHigh Bias Analysis (Expected: Many edge heads):');
analyze(arrowsHigh, gridSize);
