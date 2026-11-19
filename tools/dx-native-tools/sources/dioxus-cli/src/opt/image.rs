use anyhow::Context;
use image::codecs::jpeg::JpegEncoder;
use image::codecs::png::{CompressionType, FilterType, PngEncoder};
use image::DynamicImage;
use manganis_core::{ImageAssetOptions, ImageFormat, ImageSize};
use std::io::BufWriter;
use std::path::Path;

pub(crate) fn process_image(
    image_options: &ImageAssetOptions,
    source: &Path,
    output_path: &Path,
) -> anyhow::Result<()> {
    let mut image = image::ImageReader::new(std::io::Cursor::new(&*std::fs::read(source)?))
        .with_guessed_format()
        .context("Failed to guess image format")?
        .decode();

    if let Ok(image) = &mut image {
        if let ImageSize::Manual { width, height } = image_options.size() {
            *image = image.resize_exact(width, height, image::imageops::FilterType::Lanczos3);
        }
    }

    match (image, image_options.format()) {
        (image, ImageFormat::Png) => {
            compress_png(image.context("Failed to decode image")?, output_path)?;
        }
        (image, ImageFormat::Jpg) => {
            compress_jpg(image.context("Failed to decode image")?, output_path)?;
        }
        (Ok(image), ImageFormat::Avif) => {
            if let Err(error) = image.save(output_path) {
                tracing::error!("Failed to save avif image: {} with path {}. You must have the avif feature enabled to use avif assets", error, output_path.display());
            }
        }
        (Ok(image), ImageFormat::Webp) => {
            if let Err(err) = image.save(output_path) {
                tracing::error!("Failed to save webp image: {}. You must have the avif feature enabled to use webp assets", err);
            }
        }
        (Ok(image), _) => {
            image.save(output_path).with_context(|| {
                format!(
                    "Failed to save image (from {}) with path {}",
                    source.display(),
                    output_path.display()
                )
            })?;
        }
        // If we can't decode the image or it is of an unknown type, we just copy the file
        _ => {
            let source_file = std::fs::File::open(source).context("Failed to open source file")?;
            let mut reader = std::io::BufReader::new(source_file);
            let output_file = std::fs::File::create(output_path).with_context(|| {
                format!("Failed to create output file: {}", output_path.display())
            })?;
            let mut writer = std::io::BufWriter::new(output_file);
            std::io::copy(&mut reader, &mut writer)
                .with_context(|| {
                    format!(
                        "Failed to write image to output location: {}",
                        output_path.display()
                    )
                })
                .context("Failed to copy image data")?;
        }
    }

    Ok(())
}

// CodeBrix change: the two functions below were rewritten to use the `image` crate's own PNG and
// JPEG encoders instead of the `imagequant` and `mozjpeg` crates, so that neither crate is a
// dependency. See tools/dx-native-tools/vendoring/dioxus-cli/PATCH-NOTES.txt.

pub(crate) fn compress_png(image: DynamicImage, output_location: &Path) -> anyhow::Result<()> {
    let file = std::fs::File::create(output_location)?;
    let writer = BufWriter::new(file);
    let encoder = PngEncoder::new_with_quality(writer, CompressionType::Best, FilterType::Adaptive);
    image
        .write_with_encoder(encoder)
        .context("Failed to encode png image")?;
    Ok(())
}

pub(crate) fn compress_jpg(image: DynamicImage, output_location: &Path) -> anyhow::Result<()> {
    let file = std::fs::File::create(output_location)?;
    let writer = BufWriter::new(file);
    let encoder = JpegEncoder::new_with_quality(writer, 85);
    // JPEG has no alpha channel.
    image
        .to_rgb8()
        .write_with_encoder(encoder)
        .context("Failed to encode jpeg image")?;
    Ok(())
}
