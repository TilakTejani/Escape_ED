import { autoGenerateLevel } from './lib/generator'

const GRID_SIZES = [
  { x: 3, y: 3, z: 3 },
  { x: 5, y: 5, z: 5 },
  { x: 8, y: 8, z: 8 }
]

const DIFFICULTIES = ['easy', 'medium', 'hard'] as const

async function runBenchmark() {
  for (const size of GRID_SIZES) {
    for (const diff of DIFFICULTIES) {
      console.log(`\n--- BENCHMARK: ${size.x}x${size.y}x${size.z} (${diff}) ---`)
      const start = performance.now()
      const arrows = autoGenerateLevel(size, 0, diff)
      const end = performance.now()
      console.log(`Generated ${arrows.length} arrows in ${(end - start).toFixed(2)}ms`)
    }
  }
}

runBenchmark().catch(console.error)
