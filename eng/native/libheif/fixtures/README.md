# Native libheif and product fixtures

These tiny files contain only project-authored synthetic pixels. They contain no photograph, personal metadata, or third-party visual content. The encoded bytes are committed so normal tests never need an encoder.

| File | Format | SHA-256 |
| --- | --- | --- |
| `fovium-rgb8.heic` | Static 8-bit HEVC HEIF | `df563f66be9ad675c83a6f72706e1f10d757160596b540c59c656d1c449d00df` |
| `fovium-rgb8.avif` | Static 8-bit AVIF | `3ac263215080bfada73632b9f5155aa854fc4f1f08a3d36267d7a4b1f7d315bf` |
| `fovium-alpha8.avif` | Static 8-bit AVIF with 0/128/255 alpha bands | `bd8439ba2f3592f950d81a79da14a10e50ff5f8776b4b81e9fa437ea9a9e712e` |
| `fovium-rotate90.avif` | Static 8-bit AVIF with `irot=1` | `ba17d68144da6c1209789596bfa03be01d70a0f01cbf4e27d3a07b8912ed921d` |
| `fovium-mirror.avif` | Static 8-bit AVIF with left-to-right `imir=1` | `bd8a67d5f99339ca01ad5633925323c4ea91ec8f6ae8cde07c1565586d555757` |
| `fovium-rgb10.avif` | Static 10-bit AVIF | `ac21214471405e81cb2af199b64c9c71336be4ba2b689fc7e133570226b9d00c` |
| `fovium-pq8.avif` | Static 8-bit AVIF signaled BT.2020/PQ/BT.2020 NCL | `c6150b2d0a87d6749c53024c7f6ca7d3d352893d90173cefc7a56095df24894f` |
| `fovium-hlg8.avif` | Static 8-bit AVIF signaled BT.2020/HLG/BT.2020 NCL | `357495e28a9dbb2858754b8c0f9699b05494062f8273bd6a0f834907dbee8c65` |
| `fovium-sequence.avif` | Two-frame 8-bit AVIF sequence | `62f211cb2ef622d6ee52387f90b73da58cda0a4e5f6f49b703dfea4ae7dcae48` |
| `fovium-truncated.avif` | First 96 bytes of `fovium-rgb8.avif` | `8c07f6cd77b0407cfc84d43b19e766822db7071a1ca907a0e44dc4efcf5255d5` |

They were generated once on Windows from four solid-color quadrants using Python 3.13, Pillow 12.3.0, and pillow-heif 1.5.0:

```python
image = Image.new("RGB", (16, 12))
# Red/green/blue/yellow quadrant pixels are assigned deterministically.
image.save("fovium-rgb8.heic", format="HEIF", quality=90)
image.save("fovium-rgb8.avif", format="AVIF", quality=90)
```

The R7-C additions were generated once from PNGs written by SkiaSharp 3.119.4. The alpha pattern is `32 x 24`, red over blue with vertical alpha bands `0`, `128`, and `255`. Transform, precision, and transfer-policy fixtures use an asymmetric `20 x 12` red/green/blue/yellow quadrant pattern. The sequence contains one solid-red and one solid-blue `16 x 12` frame.

Encoding used the official libavif 1.4.2 Windows release `avifenc` with AOM 3.14.1. The generator artifact came from <https://github.com/AOMediaCodec/libavif/releases/download/v1.4.2/windows-artifacts.zip> and had SHA-256 `cb2d9fea43dcbab1d0707e3b37eb7b08070ad2fb60a2c188c39ec12382c0484a`. Relevant options were:

- common still options: `-s 10 -j 1 -q 100 -y 444 --ignore-exif --ignore-xmp --ignore-icc`;
- alpha: `--qalpha 100 -d 8 --cicp 1/13/1`;
- transforms: `-d 8 --cicp 1/13/1 --irot 1` or `--imir 1`;
- high bit depth: `-d 10 --cicp 1/13/1`;
- HDR signaling: `-d 8 --cicp 9/16/9` for PQ and `--cicp 9/18/9` for HLG;
- sequence: `-s 10 -j 1 -q 90 -d 8 -y 444 --cicp 1/13/1 --timescale 1 --duration 1 --repetition-count 0` with the two input frames.

The PQ/HLG files are signaling-policy fixtures, not mastered HDR photographs. libavif/AOM, Pillow, pillow-heif, Python, and SkiaSharp fixture tooling are generation provenance only; no encoder or extra native runtime is a production or normal-test dependency. Hosted jobs only inspect and decode the committed bytes.
