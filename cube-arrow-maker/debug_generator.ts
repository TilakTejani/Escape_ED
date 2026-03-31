import { autoGenerateLevel } from './lib/generator'
import { generateCubeGeometry } from './lib/cube'

for (const size of [3, 5, 6, 8]) {
  for (const maxLen of [3, 5]) {
    const gridSize = { x: size, y: size, z: size }
    const geometry = generateCubeGeometry(size, size, size)
    const t0 = Date.now()
    const arrows = autoGenerateLevel(gridSize, maxLen, 'medium')
    const ms = Date.now() - t0

    // Count heads on cube edges (coordinate where 2+ axes are at boundary)
    let edgeHeads = 0
    for (const arrow of arrows) {
      const headIdx = arrow.headEnd === 'end' ? arrow.path[arrow.path.length - 1] : arrow.path[0]
      const [x, y, z] = geometry.gridCoords[headIdx]
      const atBoundary = [
        x === 0 || x === size - 1,
        y === 0 || y === size - 1,
        z === 0 || z === size - 1,
      ].filter(Boolean).length
      // atBoundary >= 2 means on a cube EDGE or CORNER (not just face centre)
      if (atBoundary >= 2) edgeHeads++
    }

    const total = arrows.length
    const pct = total > 0 ? Math.round(100 * edgeHeads / total) : 0
    console.log(
      `${size}×${size}×${size} maxLen=${maxLen}: ` +
      `${edgeHeads}/${total} heads on edges (${pct}%) — ${ms}ms ` +
      `${total > 0 ? '✅' : '❌'}`
    )
  }
}
