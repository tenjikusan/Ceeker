# OCR for phone and large camera photos

HEIC and HEIF now have the same OCR controls as supported PNG/JPEG images: preview Copy text and Select text, card Copy text, batch recognition, content indexing and opt-in background recognition. Windows must have a compatible HEIF decoder installed. Unsupported/corrupt inputs show an error or are skipped by background processing.

OCR accepts sources up to 40 MB, 100 megapixels and 16,384 pixels per side. Large images are downsampled in memory to at most 4,096 pixels per side and eight megapixels before English recognition. EXIF orientation is respected and color is normalized to sRGB using the Windows decoder. The original is unchanged. Small text can become less legible after downsampling; always check recognized text.

Word coordinates refer to the oriented OCR image and are scaled into the preview. Native tests passed genuine HEIC recognition, a 12MP image downsampled to 3265x2449, word bounds, original preservation, blank/corrupt input, source-size limits and cancellation. Native HEIC preview/overlay alignment and physical touch checks remain pending.

Decoder transformation uses Microsoft's [BitmapDecoder.GetSoftwareBitmapAsync](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.imaging.bitmapdecoder.getsoftwarebitmapasync?view=winrt-26100) API.
