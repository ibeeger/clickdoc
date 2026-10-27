import CoreGraphics
import CoreImage

/// Gaussian sigma in pixels: strong enough that short text cannot be read.
public func blurSigma(imageWidth: Int) -> CGFloat {
    max(CGFloat(imageWidth) / 150, 10)
}

/// Normalizes a rect in an image/view of `size` (top-left origin), clamped to the bounds.
public func normalizedRect(_ rect: CGRect, in size: CGSize) -> CGRect {
    let clipped = rect.standardized.intersection(CGRect(origin: .zero, size: size))
    guard !clipped.isNull, size.width > 0, size.height > 0 else { return .zero }
    return CGRect(x: clipped.minX / size.width, y: clipped.minY / size.height,
                  width: clipped.width / size.width, height: clipped.height / size.height)
}

public func denormalizedRect(_ rect: CGRect, in size: CGSize) -> CGRect {
    CGRect(x: rect.minX * size.width, y: rect.minY * size.height,
           width: rect.width * size.width, height: rect.height * size.height)
}

private let ciContext = CIContext(options: [.cacheIntermediates: false])

/// Returns `image` with each normalized region (top-left origin) Gaussian-blurred.
public func applyBlur(to image: CGImage, regions: [CGRect], sigma: CGFloat? = nil) -> CGImage? {
    guard !regions.isEmpty else { return image }
    let size = CGSize(width: image.width, height: image.height)
    let input = CIImage(cgImage: image)
    let s = sigma ?? blurSigma(imageWidth: image.width)
    let blurred = input.clampedToExtent().applyingGaussianBlur(sigma: Double(s)).cropped(to: input.extent)
    var output = input
    for region in regions {
        let px = denormalizedRect(region, in: size)
        // Core Image's origin is bottom-left.
        let flipped = CGRect(x: px.minX, y: size.height - px.maxY, width: px.width, height: px.height)
        output = blurred.cropped(to: flipped).composited(over: output)
    }
    return ciContext.createCGImage(output, from: input.extent)
}
