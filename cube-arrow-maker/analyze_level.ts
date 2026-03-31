import { generateCubeGeometry, canArrowExit } from './lib/cube';
import { Arrow, CubeGeometry } from './types';

const level = {
  "gridSize": { "x": 3, "y": 3, "z": 3 },
  "arrows": [
    { "id": "1", "path": [4, 7, 6, 3, 12, 14], "headEnd": "end" },
    { "id": "2", "path": [15, 24, 23, 20, 17, 9], "headEnd": "end" },
    { "id": "3", "path": [0, 1, 10, 18, 19, 11, 13, 5, 2], "headEnd": "end" },
    { "id": "4", "path": [21, 22, 25, 16, 8], "headEnd": "end" }
  ]
};

const geometry = generateCubeGeometry(3, 3, 3);

level.arrows.forEach(arrow => {
  const headIdx = arrow.headEnd === 'end' ? arrow.path[arrow.path.length - 1] : arrow.path[0];
  const tailIdx = arrow.headEnd === 'end' ? arrow.path[arrow.path.length - 2] : arrow.path[1];
  const head = geometry.gridCoords[headIdx];
  const tail = geometry.gridCoords[tailIdx];
  const dx = head[0] - tail[0];
  const dy = head[1] - tail[1];
  const dz = head[2] - tail[2];
  
  const edgeCount = (head[0] === 0 || head[0] === 2 ? 1 : 0) + 
                    (head[1] === 0 || head[1] === 2 ? 1 : 0) + 
                    (head[2] === 0 || head[2] === 2 ? 1 : 0);

  const cgx = head[0] + dx, cgy = head[1] + dy, cgz = head[2] + dz;
  const outside = cgx < 0 || cgx >= 3 || cgy < 0 || cgy >= 3 || cgz < 0 || cgz >= 3;

  console.log("Arrow " + arrow.id + ": Head at (" + head + "), Dir (" + dx + "," + dy + "," + dz + ")");
  console.log("  Edge Count: " + edgeCount);
  console.log("  Next pos: (" + cgx + "," + cgy + "," + cgz + "), Outside: " + outside);
});
