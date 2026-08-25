# Native libheif smoke fixtures

These tiny files contain a project-authored asymmetric `16 x 12` RGB pattern. They contain no photograph, personal metadata, or third-party visual content.

| File | Format | SHA-256 |
| --- | --- | --- |
| `fovium-rgb8.heic` | Static 8-bit HEVC HEIF | `df563f66be9ad675c83a6f72706e1f10d757160596b540c59c656d1c449d00df` |
| `fovium-rgb8.avif` | Static 8-bit AVIF | `3ac263215080bfada73632b9f5155aa854fc4f1f08a3d36267d7a4b1f7d315bf` |

They were generated once on Windows from four solid-color quadrants using Python 3.13, Pillow 12.3.0, and pillow-heif 1.5.0:

```python
image = Image.new("RGB", (16, 12))
# Red/green/blue/yellow quadrant pixels are assigned deterministically.
image.save("fovium-rgb8.heic", format="HEIF", quality=90)
image.save("fovium-rgb8.avif", format="AVIF", quality=90)
```

Those encoders are fixture-generation provenance only. They are not production or native-build dependencies and are not installed by CI. Hosted jobs only decode the committed bytes.
