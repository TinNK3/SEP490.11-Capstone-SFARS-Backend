using SFARS.Domain.Common.Enum;

namespace SFARS.Application.Common;

/// <summary>
/// Application-level constants for AI inference logic.
/// These are domain values, NOT system message keys.
/// </summary>
public static class AiInferenceConstants
{
    #region YOLO Model

    /// <summary>
    /// Pipeline model identifier: Gemini Vision (binary) → YOLO Species (classification).
    /// </summary>
    public const string ModelName = "gemini-yolo-cascade-v1";

    public const string ModelVersion = "1.0.0";

    public const int DefaultTopK = 3;

    /// <summary>Sentinel model name when no inference was performed (skip photo scenario).</summary>
    public const string SkippedModelName = "None";
    public const string SkippedModelVersion = "N/A";

    #endregion

    #region Decision Rules

    public const string DecisionRuleTop1 = "Top1";
    public const string DecisionRuleSkipped = "Skipped";
    public const string DecisionRuleWoundSnakeBite = "WoundDetection_SnakeBite";
    public const string DecisionRuleWoundNotSnakeBite = "WoundDetection_NotSnakeBite";
    public const string DecisionRuleWoundNoDetection = "WoundDetection_NoWoundDetected";

    public const string UnknownSnake = "Unknown";
    public const string PredictionWoundSnakeBite = "Snake Bite";
    public const string PredictionWoundNotSnakeBite = "Not Snake Bite";
    public const string PredictionWoundNoDetection = "Wound Not Detected";

    /// <summary>
    /// Minimum confidence to display snake species to users.
    /// Below this threshold, candidates are hidden to prevent misinformation.
    /// </summary>
    public const double ConfidenceDisplayThreshold = 0.60;

    #endregion

    #region Danger Labels (display strings for SnakeRiskLevel)

    public static string GetDangerLabel(SnakeRiskLevel level) => level switch
    {
        SnakeRiskLevel.Deadly => "CỰC KỲ NGUY HIỂM",
        SnakeRiskLevel.HighlyVenomous => "NGUY HIỂM",
        SnakeRiskLevel.MildlyVenomous => "ÍT ĐỘC",
        SnakeRiskLevel.NonVenomous => "KHÔNG ĐỘC",
        _ => "CẦN KIỂM TRA"
    };

    #endregion

    #region Auto-Priority

    /// <summary>
    /// Determines the incident priority level based on AI snake detection results.
    /// Called after AnalyzeAsync and after SubmitReviewAsync (if rescuer corrects the species).
    /// </summary>
    public static SeverityLevel DetermineIncidentPriority(
        bool isSkip,
        bool isLowConfidence,
        SnakeRiskLevel? toxicityLevel)
    {
        // Skip photo or low confidence → assume danger
        if (isSkip || isLowConfidence)
            return SeverityLevel.High;

        return toxicityLevel switch
        {
            SnakeRiskLevel.Deadly => SeverityLevel.Critical,
            SnakeRiskLevel.HighlyVenomous => SeverityLevel.Critical,
            SnakeRiskLevel.MildlyVenomous => SeverityLevel.Medium,
            SnakeRiskLevel.NonVenomous => SeverityLevel.Low,
            _ => SeverityLevel.High  // Unknown or null → be cautious
        };
    }

    #endregion

    #region User Messages (Chat & Inference)

    public const string MsgIdentifyDone = "Đã hoàn tất nhận diện. Vui lòng xem kết quả bên dưới.";
    public const string MsgSkipAction = "Bạn đã bỏ qua chụp ảnh. Quy trình cứu hộ đã được kích hoạt.";
    public const string MsgNotSnakeAction = "Không phát hiện rắn trong ảnh. Ca cứu hộ đã được ghi nhận.";
    public const string MsgLowConfAction = "Chưa thể xác định loài rắn. Vui lòng áp dụng sơ cứu chung và chờ hỗ trợ.";
    public const string MsgWoundSnakeBiteAction = "Hệ thống nhận diện có dấu hiệu vết cắn rắn. Kết quả chỉ mang tính tham khảo, vui lòng chờ cứu hộ viên xác nhận.";
    public const string MsgWoundNotSnakeBiteAction = "Hệ thống chưa nhận diện dấu hiệu vết cắn rắn. Kết quả chỉ mang tính tham khảo, quá trình cứu hộ vẫn đang tiếp tục.";
    public const string MsgWoundNoDetectionAction = "Hệ thống chưa phát hiện vết thương trong ảnh. Nếu bạn chắc chắn đã bị cắn, hãy chụp lại ảnh rõ hơn hoặc mô tả trong chat.";

    public const string NoteIdentifyResult = "Đây là kết quả nhận diện sơ bộ và chỉ mang tính tham khảo. Vui lòng tham khảo ý kiến chuyên gia y tế.";
    public const string NoteIdentifyNotFound = "Hệ thống phát hiện rắn nhưng chưa tìm được thông tin loài tương ứng. Vui lòng liên hệ cứu hộ viên để được hỗ trợ.";
    public const string NoteIdentifyNotSnake = "Hệ thống xác nhận vật thể trong ảnh không phải là rắn. Nếu bạn chắc chắn đã bị rắn cắn, hãy thử chụp lại ảnh rõ hơn hoặc liên hệ đường dây nóng.";
    public const string NoteIdentifyLowConf = "Hệ thống chưa đủ thông tin để nhận diện loài rắn. Bạn có thể thử chụp lại ảnh rõ nét hơn, lấy toàn thân rắn và đảm bảo đủ ánh sáng.";
    public const string NoteIdentifyWoundResult = "Đây là kết quả phân tích vết thương sơ bộ và chỉ mang tính tham khảo. Nếu có dấu hiệu bất thường, vui lòng liên hệ ngay cơ sở y tế.";
    public const string NoteIdentifyWoundNoDetection = "Hệ thống chưa phát hiện vùng vết thương rõ ràng trong ảnh bạn gửi. Vui lòng thử chụp lại ảnh gần hơn vào vùng bị thương và đảm bảo đủ ánh sáng.";

    public const string NoteSkip = "Bạn đã bỏ qua bước chụp ảnh. Để đảm bảo an toàn, hệ thống sẽ xử lý ca này như trường hợp rắn chưa rõ loài. Hãy áp dụng sơ cứu chung và chờ cứu hộ viên hỗ trợ.";
    public const string NoteNotSnake = "Hệ thống không phát hiện rắn trong ảnh bạn gửi. Dù vậy, ca cứu hộ vẫn được ghi nhận và cứu hộ viên sẽ liên hệ bạn sớm nhất.";
    public const string NoteLowConf = "Hệ thống chưa thể xác định chính xác loài rắn từ ảnh này. Bạn có thể thử chụp lại ảnh rõ hơn (toàn thân rắn, đủ ánh sáng). Hãy áp dụng sơ cứu chung bên dưới trong lúc chờ hỗ trợ.";
    public const string NoteResult = "Đây là kết quả nhận diện sơ bộ và chỉ mang tính tham khảo. Vui lòng ưu tiên làm theo hướng dẫn sơ cứu và chỉ dẫn của cứu hộ viên.";

    public const string NoteWoundSnakeBite = "Đây là kết quả phân tích sơ bộ của AI và chỉ mang tính tham khảo. Hệ thống nhận diện hình ảnh có dấu hiệu vết cắn rắn. Vui lòng giữ bình tĩnh, làm theo hướng dẫn sơ cứu bên dưới và chờ cứu hộ viên xác nhận.";
    public const string NoteWoundNotSnakeBite = "Đây là kết quả phân tích sơ bộ của AI và chỉ mang tính tham khảo. Hệ thống chưa nhận diện dấu hiệu điển hình của vết cắn rắn từ ảnh này. Tuy nhiên, xin đừng chủ quan — hãy thực hiện sơ cứu bên dưới và chờ cứu hộ viên đánh giá chính xác.";
    public const string NoteWoundNoDetection = "Hệ thống chưa phát hiện vùng vết thương rõ ràng trong ảnh bạn gửi. Nếu bạn chắc chắn đã bị cắn, hãy thử chụp lại ảnh gần hơn vào vùng bị thương (đủ ánh sáng, không bị mờ). Ca cứu hộ vẫn được ghi nhận và cứu hộ viên sẽ liên hệ bạn sớm nhất.";

    public const string ChatInitialFormat = "Kết quả nhận diện: {0} ({1})\nĐộ tin cậy: {2}%\nMức độ: {3}\nNhóm độc: {4}\n\nSơ cứu đã được hướng dẫn ở trên.\nHãy theo dõi và báo lại nếu xuất hiện triệu chứng mới.";
    public const string ChatSkipped = "Hệ thống ghi nhận bạn đã bỏ qua bước chụp ảnh.\nĐể bảo đảm an toàn tối đa, ca cứu hộ này được xếp vào khẩn cấp vô danh (Rắn Chưa Rõ Loài).\nVui lòng tuyệt đối tuân thủ hướng dẫn Sơ cứu BẤT ĐỘNG ở mặt trước màn hình.\nNếu có bất kỳ triệu chứng nào (khó thở, sưng nhanh...), hãy nhập vào đây để AI cập nhật sơ cứu.";
    public const string ChatWoundSnakeBite = "Kết quả phân tích sơ bộ (chỉ mang tính tham khảo): Hệ thống nhận diện có dấu hiệu vết cắn rắn.\n\nTín hiệu SOS đã được phát đến các đội cứu hộ y tế lân cận.\nVui lòng làm theo hướng dẫn Sơ Cứu ở màn hình chính trong lúc chờ cứu hộ viên xác nhận.\nNếu bạn kịp nhìn thấy con vật cắn mình, hãy gửi hình ảnh hoặc mô tả vào đây để hỗ trợ cấp cứu.";
    public const string ChatWoundNotSnakeBite = "Kết quả phân tích sơ bộ (chỉ mang tính tham khảo): Hệ thống chưa nhận diện dấu hiệu điển hình của vết cắn rắn.\n\nKết quả AI có thể chưa chính xác — tín hiệu SOS vẫn đang được phân phối. Vui lòng đọc kỹ hướng dẫn Sơ cứu và báo ngay vào đây nếu xuất hiện triệu chứng bất thường (ví dụ: khó thở, sưng nhanh).";
    public const string ChatWoundNoDetection = "Hệ thống chưa phát hiện vùng vết thương rõ ràng trong ảnh bạn gửi.\n\nĐiều này có thể do ảnh chưa đủ rõ hoặc góc chụp chưa phù hợp. Nếu bạn chắc chắn đã bị cắn, hãy:\n• Chụp lại ảnh gần vùng bị thương, đảm bảo đủ ánh sáng\n• Hoặc mô tả vết thương (vị trí, kích thước, có sưng/đỏ không) vào đây\n\nTín hiệu SOS vẫn đang được tiếp tục xử lý.";
    public const string SuccessIdentifyFormat = "Nhận diện rắn: {0} (Độ tin cậy {1}%)"; // This was format expected in AiInferenceService previously? Actually, let's keep the exact string format.

    #endregion
}