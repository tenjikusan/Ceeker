# OCR third-party notices

Wormy bundles the Tesseract .NET wrapper 5.2.0, Tesseract native engine and Leptonica libraries supplied by that package, and the English model from the official tessdata_fast repository. License texts are distributed in the application's `licenses` directory.

- Wrapper: https://github.com/charlesw/tesseract — `Tesseract-wrapper.txt`.
- Engine: https://github.com/tesseract-ocr/tesseract — `Tesseract-engine.txt`.
- Image library: https://github.com/DanBloomberg/leptonica — `Leptonica.txt`.
- English model: https://github.com/tesseract-ocr/tessdata_fast — `Tesseract-model.txt`.

The bundled `eng.traineddata` SHA-256 is `7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2`. Recognition uses LSTM-only mode. The model is included in the package; no runtime model download is performed.

PDF scan recognition additionally bundles the official [tessdata_best English model](https://github.com/tesseract-ocr/tessdata_best), renamed `eng_best.traineddata`, also Apache-2.0 licensed. SHA-256: `8280AED0782FE27257A68EA10FE7EF324CA0F8D85BD2FD145D1C2B560BCB66BA`. Both models are bundled; no runtime download is performed.

PDF text extraction bundles [PdfPig 0.1.16](https://github.com/UglyToad/PdfPig), Apache-2.0 licensed, from source revision `a7bb35662bbbf405efddad50aedc9bcdcf515afc`. Its license and distribution notices are included as `licenses/PdfPig.txt` and `licenses/PdfPig-NOTICES.txt`.
Offline AI additions bundle sentence-transformers/all-MiniLM-L6-v2 at revision 10dbd2f06a8baf40ad285b037edda610c1b9a57c (Apache-2.0), Xenova CLIP ONNX exports at d15189d7028b43f1d3e65039190477f6af591c2a derived from OpenAI CLIP (MIT), and OpenCvSharp/OpenCV 4.13.0.20260627 (Apache-2.0). Hindi, Telugu and Tamil tessdata_fast models are also Apache-2.0. License texts and exact model hashes are bundled. Models run locally; no runtime model or inference requests occur.
