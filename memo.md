# memo

## voxel particle

| name | type | disp |
| --- | --- | --- |
| vert  | float3 | 深度データから取得したtriangleのcenter |
| pos   | float3 | パーティクルの位置 |
| vel   | float3 | パーティクルの速度(使用しない時もある) |
| dir   | float3 | パーティクルの方向 |
| prop  | float4 | パーティクルの状態、動作に影響する |
| t     | float  | 時間t(-x~1.0) |
| size  | float  | パーティクルの大きさ(0~x) |

## voxelParticle.prop

- x
  - 対象のvertが大きく動いたかどうか
- y
  - floating mode　通常は、上方向へ浮かんでいく、pos = pos+vel
- z
  - 光るパーティクルモード : ふわふわ光りながら消える
- w
  - 崩れるパーティクルモード : 重力で、落ちていく

## kernels

build

updatePos