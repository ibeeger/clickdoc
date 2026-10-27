import CoreGraphics
import Foundation
import ImageIO

/// Draws the click highlight onto a copy of `image`.
/// `rect` is in pixel coordinates with a top-left origin; `scale` is pixels per point.
public func renderHighlight(on image: CGImage, circleIn rect: CGRect, scale: CGFloat) -> CGImage? {
    let w = image.width, h = image.height
    guard let space = CGColorSpace(name: CGColorSpace.sRGB),
          let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                              space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    else { return nil }
    ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    // CGContext origin is bottom-left; flip the rect.
    let flipped = CGRect(x: rect.minX, y: CGFloat(h) - rect.maxY, width: rect.width, height: rect.height)
    ctx.setFillColor(red: 1, green: 0, blue: 0, alpha: 0.3)
    ctx.fillEllipse(in: flipped)
    ctx.setStrokeColor(red: 1, green: 0, blue: 0, alpha: 1)
    ctx.setLineWidth(4 * scale)
    ctx.strokeEllipse(in: flipped)
    return ctx.makeImage()
}

public func pngData(from image: CGImage) -> Data? {
    let data = NSMutableData()
    guard let dest = CGImageDestinationCreateWithData(data, "public.png" as CFString, 1, nil) else { return nil }
    CGImageDestinationAddImage(dest, image, nil)
    guard CGImageDestinationFinalize(dest) else { return nil }
    return data as Data
}

/// Downscaled decode of a PNG, so list thumbnails do not hold full-resolution bitmaps.
public func thumbnailImage(fromPNG data: Data, maxPixelSize: Int) -> CGImage? {
    guard let source = CGImageSourceCreateWithData(data as CFData, nil) else { return nil }
    let options: [CFString: Any] = [
        kCGImageSourceCreateThumbnailFromImageAlways: true,
        kCGImageSourceThumbnailMaxPixelSize: maxPixelSize,
        kCGImageSourceCreateThumbnailWithTransform: true,
    ]
    return CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary)
}
