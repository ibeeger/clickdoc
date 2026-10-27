// Builds a macOS .iconset from a square-ish PNG: center-crops to a square, applies the
// standard rounded-rect icon shape with transparent margins and a soft shadow.
// Usage: make-icon <input.png> <output.iconset>
import CoreGraphics
import Foundation
import ImageIO

let args = CommandLine.arguments
guard args.count == 3,
      let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: args[1]) as CFURL, nil),
      let original = CGImageSourceCreateImageAtIndex(src, 0, nil) else {
    FileHandle.standardError.write(Data("usage: make-icon <input.png> <output.iconset>\n".utf8))
    exit(1)
}
let side = min(original.width, original.height)
let crop = CGRect(x: (original.width - side) / 2, y: (original.height - side) / 2, width: side, height: side)
guard let square = original.cropping(to: crop) else { exit(1) }

let outDir = URL(fileURLWithPath: args[2], isDirectory: true)
try? FileManager.default.removeItem(at: outDir)
try FileManager.default.createDirectory(at: outDir, withIntermediateDirectories: true)

func render(_ size: Int) -> CGImage {
    let s = CGFloat(size)
    let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpace(name: CGColorSpace.sRGB)!,
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.interpolationQuality = .high
    // Apple's macOS icon grid: 824/1024 body, corner radius ~22.37% of the body.
    let body = s * 824 / 1024
    let rect = CGRect(x: (s - body) / 2, y: (s - body) / 2 + s * 0.006, width: body, height: body)
    let path = CGPath(roundedRect: rect, cornerWidth: body * 0.2237, cornerHeight: body * 0.2237, transform: nil)
    ctx.saveGState()
    ctx.setShadow(offset: CGSize(width: 0, height: -s * 0.01), blur: s * 0.025,
                  color: CGColor(gray: 0, alpha: 0.35))
    ctx.addPath(path)
    ctx.setFillColor(CGColor(gray: 0, alpha: 1))
    ctx.fillPath()
    ctx.restoreGState()
    ctx.addPath(path)
    ctx.clip()
    ctx.draw(square, in: rect)
    return ctx.makeImage()!
}

for base in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let name = scale == 1 ? "icon_\(base)x\(base).png" : "icon_\(base)x\(base)@2x.png"
        let url = outDir.appendingPathComponent(name) as CFURL
        let dest = CGImageDestinationCreateWithURL(url, "public.png" as CFString, 1, nil)!
        CGImageDestinationAddImage(dest, render(base * scale), nil)
        guard CGImageDestinationFinalize(dest) else { exit(1) }
    }
}
