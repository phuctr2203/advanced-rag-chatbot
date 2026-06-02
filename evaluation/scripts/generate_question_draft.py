import json
import re
import sys
from collections import Counter
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[2]
EXTRACTED = ROOT / "evaluation" / "corpus" / "extracted"
OUTPUT = ROOT / "evaluation" / "datasets" / "rag-evaluation-v1.draft.json"


def load_documents() -> dict[str, dict]:
    documents = {}
    for path in EXTRACTED.glob("*.json"):
        document = json.loads(path.read_text(encoding="utf-8"))
        documents[document["sourceFile"]] = document
    return documents


def compact(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def select_excerpt(documents: dict[str, dict], source_file: str, keywords: list[str], page: int | None = None) -> tuple[int, str]:
    chunks = [chunk for chunk in documents[source_file]["chunks"] if chunk.get("chunkType") == "text"]
    if page is not None:
        page_chunks = [chunk for chunk in chunks if int(chunk.get("pageNumber", 0)) == page]
        if page_chunks:
            chunks = page_chunks
    if not chunks:
        return page or 1, ""

    lowered_keywords = [keyword.casefold() for keyword in keywords]
    def score(chunk: dict) -> int:
        text = chunk.get("text", "")
        lowered = text.casefold()
        keyword_score = sum(lowered.count(keyword) * 12 for keyword in lowered_keywords)
        contents_penalty = 0
        if "table of contents" in lowered or "mục lục" in lowered or text.count("...") >= 3:
            contents_penalty = 25
        return keyword_score - contents_penalty

    best = max(chunks, key=score)
    text = compact(best.get("text", ""))
    lowered_text = text.casefold()
    positions = [lowered_text.find(keyword) for keyword in lowered_keywords if lowered_text.find(keyword) >= 0]
    start = max(0, min(positions) - 180) if positions else 0
    excerpt = text[start : start + 900]
    return int(best.get("pageNumber", page or 1)), excerpt


def policy(source: str, language: str, question_type: str, question: str, keywords: list[str], tags: list[str] | None = None) -> dict:
    return {
        "scenario": "grounded_text_policy",
        "domain": domain_for(source),
        "language": language,
        "questionType": question_type,
        "question": question,
        "sourceFile": source,
        "keywords": keywords,
        "tags": tags or [],
    }


def domain_for(source: str) -> str:
    if source in {
        "CII Regulation - Building Handbook (Eng).pdf",
        "POL-LE-006-Office Building Regulations Vietnam.pdf",
        "SỔ TAY KHẨN CẤP - CII TOWER (final).pdf",
        "SỔ TAY TOÀ NHÀ - CII TOWER (final).pdf",
        "CII Tower No-Smoking Policy Reminders(Vietnamese).pdf",
        "ELCA Vietnam Offices – Bicycle Parking [VN-EN].pdf",
    }:
        return "CII_TOWER_SUPPORT"
    if source in {
        "Payment process V2_2023.pdf",
        "POL-SP-010-Acceptable Use of Assets Vietnam.pdf",
        "POL-LE-005-Code of Conduct Vietnam.pdf",
        "ELCA VN_Oracle_Guidelines for newcomers.pdf",
        "How to Refer a Candidate on Oracle.docx",
        "EmployeeNonDisclosureAgreement.doc",
        "Employees Laptop Buy- Back Scheme.pdf",
        "Payment request form.docx",
        "Internal purchasing form.xlsx",
    }:
        return "ELCA_GENERAL"
    return "ELCA_HR"


POLICY_ROWS = [
    # English: 36 rows
    policy("Attribution of additional annual leave days with seniority.pdf", "en", "numeric_fact", "How many paid annual leave days does an employee receive after completing 12 months of service?", ["12 months", "18 working days"], ["annual_leave"]),
    policy("Attribution of additional annual leave days with seniority.pdf", "en", "numeric_fact", "How does continuous service increase the annual leave entitlement?", ["01 day", "05 years"], ["annual_leave", "seniority"]),
    policy("Attribution of additional annual leave days with seniority.pdf", "en", "eligibility", "How is annual leave calculated for employees with a special annual leave entitlement?", ["Special Annual Entitlement", "highest"], ["annual_leave"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "en", "direct_fact", "What are the company's normal working days?", ["Article 1", "Working days"], ["working_hours"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "en", "numeric_fact", "What are the standard daily working hours?", ["Article 2", "Working hours"], ["working_hours"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "en", "procedure", "What approval is required before employees work overtime?", ["overtime", "approval"], ["overtime"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "en", "numeric_fact", "What limits apply to overtime hours?", ["overtime", "hours"], ["overtime"]),
    policy("POL-HR-013-Work-From-Home Policy Vietnam.pdf", "en", "eligibility", "Who is eligible to work from home?", ["eligible", "work from home"], ["wfh"]),
    policy("POL-HR-013-Work-From-Home Policy Vietnam.pdf", "en", "procedure", "How should an employee request a work-from-home day?", ["request", "Work From Home"], ["wfh"]),
    policy("POL-HR-013-Work-From-Home Policy Vietnam.pdf", "en", "prohibition", "What responsibilities apply when working from home?", ["responsibilities", "Work From Home"], ["wfh"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "en", "direct_fact", "What is on-call support?", ["on-call support"], ["on_call"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "en", "comparison", "How does production monitoring differ from on-call support?", ["Production Monitoring", "On-Call Support"], ["on_call"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "en", "procedure", "What planning information must be prepared for on-call support?", ["planning", "on-call support"], ["on_call"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "en", "procedure", "How should actual on-call support time be recorded?", ["actual time", "on-call"], ["on_call"]),
    policy("Referral Bonus Scheme.pdf", "en", "eligibility", "Who can receive an employee referral bonus?", ["eligible", "referral"], ["referral"]),
    policy("Referral Bonus Scheme.pdf", "en", "procedure", "What conditions must be met before a referral bonus is paid?", ["bonus", "paid"], ["referral"]),
    policy("CII Regulation - Building Handbook (Eng).pdf", "en", "procedure", "What should tenants do when they discover a fire at CII Tower?", ["fire", "emergency"], ["cii", "fire"]),
    policy("Payment process V2_2023.pdf", "en", "procedure", "What documents are required for a payment request?", ["REQUIREMENT OF DOCUMENTS"], ["payment"]),
    policy("Payment process V2_2023.pdf", "en", "procedure", "How do I submit a payment request?", ["PROCESS", "PAYMENT"], ["payment"]),
    policy("Payment process V2_2023.pdf", "en", "numeric_fact", "What is the limit for cash payments?", ["cash", "VND"], ["payment"]),
    policy("Payment process V2_2023.pdf", "en", "procedure", "What invoice requirements apply to a payment request?", ["invoice"], ["payment"]),
    policy("POL-SP-010-Acceptable Use of Assets Vietnam.pdf", "en", "prohibition", "What restrictions apply to installing software on company assets?", ["software"], ["acceptable_use"]),
    policy("POL-SP-010-Acceptable Use of Assets Vietnam.pdf", "en", "prohibition", "What rules apply to employee passwords?", ["password"], ["acceptable_use"]),
    policy("POL-SP-010-Acceptable Use of Assets Vietnam.pdf", "en", "prohibition", "What restrictions apply to using company email and internet access?", ["email", "internet"], ["acceptable_use"]),
    policy("POL-LE-005-Code of Conduct Vietnam.pdf", "en", "prohibition", "What does the code of conduct say about conflicts of interest?", ["conflict of interest"], ["conduct"]),
    policy("POL-LE-005-Code of Conduct Vietnam.pdf", "en", "prohibition", "What does the code of conduct say about bribery or improper advantages?", ["bribery"], ["conduct"]),
    policy("POL-LE-005-Code of Conduct Vietnam.pdf", "en", "prohibition", "How should employees handle confidential information?", ["confidential"], ["conduct"]),
    policy("POL-LE-006-Office Building Regulations Vietnam.pdf", "en", "procedure", "What rules apply when bringing goods or equipment into or out of the building?", ["goods", "equipment"], ["cii", "access"]),
    policy("Employees Laptop Buy- Back Scheme.pdf", "en", "eligibility", "Who is eligible for the employee laptop buy-back scheme?", ["eligible", "laptop"], ["laptop_buyback"]),
    policy("CII Regulation - Building Handbook (Eng).pdf", "en", "prohibition", "What fire-safety rules apply inside CII Tower?", ["FIRE", "protection"], ["cii", "fire"]),
    policy("CII Regulation - Building Handbook (Eng).pdf", "en", "direct_fact", "What are the operating hours of the CII Tower parking area?", ["06:00", "22:00"], ["cii"]),
    policy("CII Regulation - Building Handbook (Eng).pdf", "en", "procedure", "How should a building incident be reported?", ["emergency", "Property Manager"], ["cii"]),
    policy("POL-LE-006-Office Building Regulations Vietnam.pdf", "en", "prohibition", "What rules apply to smoking in the office building?", ["smoking"], ["cii"]),
    policy("POL-LE-006-Office Building Regulations Vietnam.pdf", "en", "procedure", "When may construction work that causes inconvenience be performed?", ["19:00", "construction"], ["cii"]),
    policy("ELCA VN_Oracle_Guidelines for newcomers.pdf", "en", "procedure", "How does a newcomer reset an Oracle password?", ["Reset password", "Submit"], ["oracle"]),
    policy("How to Refer a Candidate on Oracle.docx", "en", "procedure", "How do I refer a candidate on Oracle?", ["Current jobs", "Step 2"], ["oracle", "referral"]),

    # Vietnamese: 18 rows
    policy("Attribution of additional annual leave days with seniority.pdf", "vi", "numeric_fact", "Nhân viên làm việc đủ 12 tháng được hưởng bao nhiêu ngày nghỉ phép năm có lương?", ["12 months", "18 working days"], ["annual_leave", "multilingual_parity"]),
    policy("Attribution of additional annual leave days with seniority.pdf", "vi", "numeric_fact", "Cứ mỗi 5 năm làm việc liên tục thì số ngày nghỉ phép tăng thêm bao nhiêu?", ["01 day", "05 years"], ["annual_leave"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "vi", "direct_fact", "Thời gian làm việc hàng ngày của công ty là mấy giờ?", ["Article 2", "Working hours"], ["working_hours", "multilingual_parity"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "vi", "procedure", "Nhân viên cần làm gì trước khi làm thêm giờ?", ["overtime", "approval"], ["overtime"]),
    policy("POL-HR-013-Work-From-Home Policy Vietnam.pdf", "vi", "procedure", "Nhân viên phải đăng ký làm việc tại nhà như thế nào?", ["request", "Work From Home"], ["wfh"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "vi", "comparison", "Hỗ trợ trực và giám sát hệ thống sản xuất khác nhau như thế nào?", ["Production Monitoring", "On-Call Support"], ["on_call"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "vi", "procedure", "Thời gian hỗ trợ trực thực tế phải được ghi nhận như thế nào?", ["actual time", "on-call"], ["on_call"]),
    policy("Referral Bonus Scheme.pdf", "vi", "eligibility", "Ai đủ điều kiện nhận thưởng giới thiệu ứng viên?", ["eligible", "referral"], ["referral"]),
    policy("Payment process V2_2023.pdf", "vi", "procedure", "Hồ sơ đề nghị thanh toán cần những chứng từ gì?", ["REQUIREMENT OF DOCUMENTS"], ["payment"]),
    policy("Payment process V2_2023.pdf", "vi", "numeric_fact", "Giới hạn thanh toán bằng tiền mặt là bao nhiêu?", ["cash", "VND"], ["payment"]),
    policy("PIT depedent register (Guidance 2026).pdf", "vi", "procedure", "Hồ sơ đăng ký người phụ thuộc năm 2026 cần những giấy tờ gì?", ["20-ĐK-TCT", "ủy quyền"], ["pit"]),
    policy("SỔ TAY TOÀ NHÀ - CII TOWER (final).pdf", "vi", "procedure", "Khi mang hàng hóa hoặc thiết bị ra vào tòa nhà cần tuân thủ quy định gì?", ["hàng hóa"], ["cii", "access"]),
    policy("SỔ TAY KHẨN CẤP - CII TOWER (final).pdf", "vi", "procedure", "Khi phát hiện cháy tại CII Tower cần xử lý như thế nào?", ["cháy"], ["cii", "fire"]),
    policy("POL-LE-005-Code of Conduct Vietnam.pdf", "vi", "prohibition", "Quy tắc ứng xử quy định thế nào về xung đột lợi ích?", ["conflict of interest"], ["conduct"]),
    policy("POL-LE-006-Office Building Regulations Vietnam.pdf", "vi", "prohibition", "Tòa nhà quy định như thế nào về việc hút thuốc?", ["smoking"], ["cii"]),
    policy("SỔ TAY TOÀ NHÀ - CII TOWER (final).pdf", "vi", "direct_fact", "Bãi xe của tòa nhà CII hoạt động trong khung giờ nào?", ["06g00", "22g00"], ["cii"]),
    policy("SỔ TAY KHẨN CẤP - CII TOWER (final).pdf", "vi", "procedure", "Khi có tình huống khẩn cấp tại CII Tower thì cần báo cho ai?", ["khẩn cấp"], ["cii"]),
    policy("ELCA VN_Oracle_Guidelines for newcomers.pdf", "vi", "procedure", "Nhân viên mới phải làm gì để đặt lại mật khẩu Oracle?", ["Reset password", "Submit"], ["oracle"]),

    # French: 9 rows
    policy("Attribution of additional annual leave days with seniority.pdf", "fr", "numeric_fact", "Combien de jours de congé annuel payé reçoit un employé après 12 mois de service ?", ["12 months", "18 working days"], ["annual_leave", "multilingual_parity"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "fr", "direct_fact", "Quels sont les horaires de travail quotidiens habituels ?", ["Article 2", "Working hours"], ["working_hours"]),
    policy("POL-HR-013-Work-From-Home Policy Vietnam.pdf", "fr", "procedure", "Comment un employé doit-il demander une journée de télétravail ?", ["request", "Work From Home"], ["wfh"]),
    policy("Referral Bonus Scheme.pdf", "fr", "eligibility", "Qui peut recevoir une prime de recommandation d'un candidat ?", ["eligible", "referral"], ["referral"]),
    policy("Payment process V2_2023.pdf", "fr", "numeric_fact", "Quelle est la limite pour les paiements en espèces ?", ["cash", "VND"], ["payment"]),
    policy("POL-SP-010-Acceptable Use of Assets Vietnam.pdf", "fr", "prohibition", "Quelles restrictions s'appliquent à l'installation de logiciels sur les équipements de l'entreprise ?", ["software"], ["acceptable_use"]),
    policy("POL-LE-005-Code of Conduct Vietnam.pdf", "fr", "prohibition", "Que dit le code de conduite au sujet des conflits d'intérêts ?", ["conflict of interest"], ["conduct"]),
    policy("CII Regulation - Building Handbook (Eng).pdf", "fr", "direct_fact", "Quels sont les horaires d'ouverture du parking de la tour CII ?", ["06:00", "22:00"], ["cii"]),
    policy("POL-LE-006-Office Building Regulations Vietnam.pdf", "fr", "procedure", "Quand les travaux de construction gênants peuvent-ils être effectués dans l'immeuble ?", ["19:00", "construction"], ["cii"]),

    # German: 9 rows
    policy("Attribution of additional annual leave days with seniority.pdf", "de", "numeric_fact", "Wie viele bezahlte Urlaubstage erhält ein Mitarbeiter nach zwölf Monaten Betriebszugehörigkeit?", ["12 months", "18 working days"], ["annual_leave", "multilingual_parity"]),
    policy("POL-HR-012-Working Days and Hours Vietnam.pdf", "de", "procedure", "Welche Genehmigung ist vor Überstunden erforderlich?", ["overtime", "approval"], ["overtime"]),
    policy("POL-HR-013-Work-From-Home Policy Vietnam.pdf", "de", "eligibility", "Wer darf von zu Hause aus arbeiten?", ["eligible", "work from home"], ["wfh"]),
    policy("POL-HR-014-On-call Support Regulation Vietnam.pdf", "de", "comparison", "Wie unterscheidet sich Produktionsüberwachung vom Bereitschaftsdienst?", ["Production Monitoring", "On-Call Support"], ["on_call"]),
    policy("Referral Bonus Scheme.pdf", "de", "procedure", "Welche Bedingungen müssen erfüllt sein, bevor ein Empfehlungsbonus ausgezahlt wird?", ["bonus", "paid"], ["referral"]),
    policy("Payment process V2_2023.pdf", "de", "procedure", "Welche Unterlagen werden für einen Zahlungsantrag benötigt?", ["REQUIREMENT OF DOCUMENTS"], ["payment"]),
    policy("POL-SP-010-Acceptable Use of Assets Vietnam.pdf", "de", "prohibition", "Welche Regeln gelten für Passwörter auf Unternehmenssystemen?", ["password"], ["acceptable_use"]),
    policy("POL-LE-006-Office Building Regulations Vietnam.pdf", "de", "procedure", "Wie melde ich ein Gebäudeproblem?", ["emergency", "Property Manager"], ["cii"]),
    policy("CII Regulation - Building Handbook (Eng).pdf", "de", "direct_fact", "Zu welchen Zeiten ist der Parkplatz im CII Tower geöffnet?", ["06:00", "22:00"], ["cii"]),
]

FORM_ROWS = [
    ("en", "How do I download the payment request form?", "Payment request form.docx", "payment-request-form"),
    ("en", "Which form should I use for an internal purchasing request?", "Internal purchasing form.xlsx", "internal-purchasing-form"),
    ("en", "Where can I get the overtime request template?", "Overtime Form.xlsx", "overtime-form"),
    ("en", "Which mission form is used for on-call support?", "MissionForm_OnCallSupport.xlsx", "on-call-support-form"),
    ("en", "Which mission form is used for production monitoring?", "MissionForm_ProductionMonitoring.xlsx", "production-monitoring-form"),
    ("en", "Which form should be used to register a dependent under the current guidance?", "PIT Dependent register 2025 (Form 20-ĐK-TCT) - Form đăng ký.docx", "pit-dependent-registration"),
    ("vi", "Tôi tải mẫu giấy đề nghị thanh toán ở đâu?", "Payment request form.docx", "payment-request-form"),
    ("vi", "Tôi cần dùng biểu mẫu nào để đăng ký làm thêm giờ?", "Overtime Form.xlsx", "overtime-form"),
    ("vi", "Biểu mẫu nhiệm vụ nào dùng cho hỗ trợ trực?", "MissionForm_OnCallSupport.xlsx", "on-call-support-form"),
    ("vi", "Đăng ký người phụ thuộc hiện nay sử dụng mẫu nào?", "PIT Dependent register 2025 (Form 20-ĐK-TCT) - Form đăng ký.docx", "pit-dependent-registration"),
    ("fr", "Quel formulaire faut-il utiliser pour une demande de paiement ?", "Payment request form.docx", "payment-request-form"),
    ("de", "Welches Formular soll ich für Überstunden verwenden?", "Overtime Form.xlsx", "overtime-form"),
]

IMAGE_ROWS = [
    ("en", "What does the CII emergency response flowchart show?", "CII Regulation - Building Handbook (Eng).pdf", 27, "cii-emergency-flowchart"),
    ("en", "Show me the Oracle newcomer password reset screenshot.", "ELCA VN_Oracle_Guidelines for newcomers.pdf", 2, "oracle-password-reset-screenshot"),
    ("vi", "Biển nhắc nhở cấm hút thuốc tại CII Tower ghi gì?", "CII Tower No-Smoking Policy Reminders(Vietnamese).pdf", 1, "cii-no-smoking-reminder"),
    ("vi", "Cho tôi xem thông báo về chỗ để xe đạp tại văn phòng ELCA Việt Nam.", "ELCA Vietnam Offices – Bicycle Parking [VN-EN].pdf", 1, "bicycle-parking-notice"),
]

SMALLTALK_ROWS = [("en", "Hi"), ("vi", "Xin chào"), ("fr", "Bonjour"), ("de", "Hallo")]
OUT_OF_SCOPE_ROWS = [
    ("en", "What is the capital of France?"),
    ("vi", "Thủ đô của Nhật Bản là gì?"),
    ("fr", "Quel temps fera-t-il demain ?"),
    ("de", "Wer hat die Fußball-Weltmeisterschaft gewonnen?"),
]
MIXED_ROWS = [
    ("en", "Hi, what is the overtime policy?", "POL-HR-012-Working Days and Hours Vietnam.pdf", ["overtime"]),
    ("vi", "Xin chào, thời gian làm việc hàng ngày là mấy giờ?", "POL-HR-012-Working Days and Hours Vietnam.pdf", ["Working hours"]),
    ("fr", "Bonjour, quelle est la limite pour les paiements en espèces ?", "Payment process V2_2023.pdf", ["cash", "VND"]),
    ("de", "Hallo, wie melde ich ein Gebäudeproblem?", "CII Regulation - Building Handbook (Eng).pdf", ["emergency", "Property Manager"]),
]


def grounded_row(documents: dict[str, dict], index: int, definition: dict) -> dict:
    page, excerpt = select_excerpt(documents, definition["sourceFile"], definition["keywords"])
    matched_keywords = [keyword for keyword in definition["keywords"] if keyword.casefold() in excerpt.casefold()]
    return {
        "id": f"eval-{index:03d}",
        "scenario": definition["scenario"],
        "domain": definition["domain"],
        "language": definition["language"],
        "questionType": definition["questionType"],
        "question": definition["question"],
        "referenceAnswer": excerpt,
        "expectedIntent": "POLICY_QUERY",
        "expectedSources": [{"file": definition["sourceFile"], "page": page}],
        "referenceContexts": [excerpt],
        "expectedFormDownload": None,
        "expectedImage": False,
        "tags": definition["tags"],
        "draftReferenceKeywords": definition["keywords"],
        "draftMatchedKeywords": matched_keywords,
        "reviewStatus": "draft_requires_review",
    }


def main() -> None:
    documents = load_documents()
    rows = []
    index = 1

    for definition in POLICY_ROWS:
        rows.append(grounded_row(documents, index, definition))
        index += 1

    for language, question, source_file, group in FORM_ROWS:
        page, excerpt = select_excerpt(documents, source_file, [group.replace("-", " ")])
        rows.append(
            {
                "id": f"eval-{index:03d}",
                "scenario": "form_template_surfacing",
                "domain": domain_for(source_file),
                "language": language,
                "questionType": "form_download",
                "question": question,
                "referenceAnswer": excerpt,
                "expectedIntent": "POLICY_QUERY",
                "expectedSources": [{"file": source_file, "page": page}],
                "referenceContexts": [excerpt],
                "expectedFormDownload": source_file,
                "expectedImage": False,
                "tags": ["form_download", group],
                "reviewStatus": "draft_requires_review",
            }
        )
        index += 1

    for language, question, source_file, page, tag in IMAGE_ROWS:
        _, excerpt = select_excerpt(documents, source_file, [], page=page)
        rows.append(
            {
                "id": f"eval-{index:03d}",
                "scenario": "image_surfacing",
                "domain": domain_for(source_file),
                "language": language,
                "questionType": "image_surface",
                "question": question,
                "referenceAnswer": excerpt or "Image caption must be reviewed after ingestion with image captioning enabled.",
                "expectedIntent": "POLICY_QUERY",
                "expectedSources": [{"file": source_file, "page": page}],
                "referenceContexts": [excerpt] if excerpt else [],
                "expectedFormDownload": None,
                "expectedImage": True,
                "tags": ["image_surface", tag],
                "reviewStatus": "draft_requires_review",
            }
        )
        index += 1

    for language, question in SMALLTALK_ROWS:
        rows.append(control_row(index, "smalltalk", language, question, "SMALLTALK"))
        index += 1
    for language, question in OUT_OF_SCOPE_ROWS:
        rows.append(control_row(index, "out_of_scope", language, question, "OUT_OF_SCOPE"))
        index += 1
    for language, question, source_file, keywords in MIXED_ROWS:
        page, excerpt = select_excerpt(documents, source_file, keywords)
        matched_keywords = [keyword for keyword in keywords if keyword.casefold() in excerpt.casefold()]
        rows.append(
            {
                "id": f"eval-{index:03d}",
                "scenario": "mixed_intent",
                "domain": domain_for(source_file),
                "language": language,
                "questionType": "mixed_intent",
                "question": question,
                "referenceAnswer": excerpt,
                "expectedIntent": "POLICY_QUERY",
                "expectedSources": [{"file": source_file, "page": page}],
                "referenceContexts": [excerpt],
                "expectedFormDownload": None,
                "expectedImage": False,
                "tags": ["mixed_intent"],
                "draftReferenceKeywords": keywords,
                "draftMatchedKeywords": matched_keywords,
                "reviewStatus": "draft_requires_review",
            }
        )
        index += 1

    assert len(POLICY_ROWS) == 72
    assert len(rows) == 100
    assert Counter(row["language"] for row in rows) == Counter({"en": 47, "vi": 27, "fr": 13, "de": 13})
    assert Counter(row["scenario"] for row in rows) == Counter(
        {
            "grounded_text_policy": 72,
            "form_template_surfacing": 12,
            "image_surfacing": 4,
            "smalltalk": 4,
            "out_of_scope": 4,
            "mixed_intent": 4,
        }
    )
    assert all(row["referenceAnswer"] for row in rows)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Wrote {len(rows)} draft questions to {OUTPUT}")
    print("Scenarios:", dict(Counter(row["scenario"] for row in rows)))
    print("Languages:", dict(Counter(row["language"] for row in rows)))
    print("Domains:", dict(Counter(row["domain"] for row in rows)))


def control_row(index: int, scenario: str, language: str, question: str, intent: str) -> dict:
    return {
        "id": f"eval-{index:03d}",
        "scenario": scenario,
        "domain": "CONTROL",
        "language": language,
        "questionType": scenario,
        "question": question,
        "referenceAnswer": "No grounded policy answer is expected.",
        "expectedIntent": intent,
        "expectedSources": [],
        "referenceContexts": [],
        "expectedFormDownload": None,
        "expectedImage": False,
        "tags": [scenario, "retrieval_bypass"],
        "reviewStatus": "draft_requires_review",
    }


if __name__ == "__main__":
    main()
