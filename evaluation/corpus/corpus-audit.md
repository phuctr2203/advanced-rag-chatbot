# Corpus Audit

Generated from the application parser endpoints with image captioning disabled.

## Summary

| Measure | Count |
|---|---:|
| Corpus files | 44 |
| Extracted successfully | 42 |
| Extraction errors | 2 |
| Empty readable text | 10 |

## Reviewed Findings

- All 44 source files were inspected through the application parser workflow.
- Two legacy PIT dependent-registration DOC forms still fail LibreOffice conversion and remain excluded until replaced or repaired.
- Ten PDFs are image-only or scanned. Most have newer searchable counterparts; unique notice documents remain candidates for image-surfacing evaluation.
- Canonical current policies are identified separately from superseded, duplicate, scanned, form-template, and historical-data variants.
- The audit fixed headless DOC and PPTX conversion by assigning each LibreOffice run an isolated user profile.

## Document Inventory

| File | Type | Status | Pages or sheets | Text chunks | Text length | PDF image candidates | Suggested domain | Suggested role |
|---|---|---|---:|---:|---:|---:|---|---|
| AcceptableUseOfAssetsVN_EN.pdf | .pdf | ok | 0 | 0 | 0 | 10 | ELCA_GENERAL | policy |
| Attribution of additional annual leave days with seniority.pdf | .pdf | ok | 5 | 5 | 7279 | 0 | ELCA_HR | policy |
| CII Regulation - Building Handbook (Eng).pdf | .pdf | ok | 30 | 30 | 59344 | 2 | CII_TOWER_SUPPORT | guidance |
| CII Tower No-Smoking Policy Reminders(Vietnamese).pdf | .pdf | ok | 0 | 0 | 0 | 1 | CII_TOWER_SUPPORT | policy |
| Code of Conduct [EN].pdf | .pdf | ok | 0 | 0 | 0 | 10 | ELCA_GENERAL | policy |
| ELCA Vietnam Offices – Bicycle Parking [VN-EN].pdf | .pdf | ok | 0 | 0 | 0 | 2 | CII_TOWER_SUPPORT | policy |
| ELCA VN_Oracle_Guidelines for newcomers.pdf | .pdf | ok | 14 | 14 | 5409 | 24 | ELCA_GENERAL | guidance |
| EmployeeNonDisclosureAgreement.doc | .doc | ok | 6 | 48 | 8567 | 0 | ELCA_GENERAL | policy |
| Employees Laptop Buy- Back Scheme.pdf | .pdf | ok | 6 | 6 | 8426 | 0 | ELCA_GENERAL | policy |
| How to Refer a Candidate on Oracle.docx | .docx | ok | 2 | 11 | 789 | 0 | ELCA_GENERAL | policy |
| Internal purchasing form.xlsx | .xlsx | ok | 1 | 1 | 967 | 0 | ELCA_GENERAL | form |
| InternalLabor_Regulation_VN_EN.pdf | .pdf | ok | 0 | 0 | 0 | 71 | ELCA_HR | policy |
| laptop buy-back regulation.pdf | .pdf | ok | 0 | 0 | 0 | 3 | ELCA_GENERAL | policy |
| MissionForm_OnCallSupport.xlsx | .xlsx | ok | 6 | 6 | 14700 | 0 | ELCA_HR | form |
| MissionForm_ProductionMonitoring.xlsx | .xlsx | ok | 6 | 6 | 8444 | 0 | ELCA_GENERAL | form |
| OnCallSupport_Regulation.pdf | .pdf | ok | 0 | 0 | 0 | 72 | ELCA_HR | policy |
| Overtime Form.xlsx | .xlsx | ok | 6 | 6 | 8328 | 0 | ELCA_HR | form |
| Overtime request & actual 2024.xlsx | .xlsx | ok | 24 | 24 | 57545 | 0 | ELCA_HR | form |
| Payment process V2_2023.pdf | .pdf | ok | 11 | 11 | 14962 | 0 | ELCA_GENERAL | policy |
| Payment request form.docx | .docx | ok | 12 | 60 | 2589 | 0 | ELCA_GENERAL | form |
| PIT depedent register (Form 07.XN.NPT.TNCN) - Confirmation of direct nurturing of dependent.docx | .docx | ok | 3 | 19 | 1230 | 0 | ELCA_HR | form |
| PIT depedent register (Guidance 2024).docx | .docx | ok | 8 | 48 | 4250 | 0 | ELCA_HR | guidance |
| PIT depedent register (Guidance 2024).pdf | .pdf | ok | 3 | 3 | 4666 | 0 | ELCA_HR | guidance |
| PIT depedent register (Guidance 2026).pdf | .pdf | ok | 3 | 3 | 4711 | 0 | ELCA_HR | guidance |
| PIT dependent register - Power of attorney for registration of dependent.doc | .doc | error | 0 | 0 | 0 | 0 | ELCA_HR | policy |
| PIT Dependent register 2025 (Form 20-ĐK-TCT) - Form đăng ký.docx | .docx | ok | 2 | 11 | 743 | 0 | ELCA_HR | form |
| PIT dependent regiter (Form 07.ĐK.NPT.TNCN).doc | .doc | error | 0 | 0 | 0 | 0 | ELCA_HR | form |
| PITDependentRegister_Form20DKTCT.docx | .docx | ok | 1 | 4 | 464 | 0 | ELCA_HR | form |
| PITDependentRegister_Guidance.pdf | .pdf | ok | 1 | 1 | 3018 | 0 | ELCA_HR | guidance |
| POL-HR-011-Internal Labour Regulations Vietnam.pdf | .pdf | ok | 66 | 66 | 175718 | 0 | ELCA_HR | policy |
| POL-HR-012-Working Days and Hours Vietnam.pdf | .pdf | ok | 8 | 8 | 14044 | 0 | ELCA_HR | policy |
| POL-HR-013-Work-From-Home Policy Vietnam.pdf | .pdf | ok | 5 | 5 | 6254 | 0 | ELCA_HR | policy |
| POL-HR-014-On-call Support Regulation Vietnam.pdf | .pdf | ok | 24 | 24 | 44134 | 0 | ELCA_HR | policy |
| POL-LE-005-Code of Conduct Vietnam.pdf | .pdf | ok | 9 | 9 | 19449 | 0 | ELCA_GENERAL | policy |
| POL-LE-006-Office Building Regulations Vietnam.pdf | .pdf | ok | 31 | 31 | 61594 | 2 | CII_TOWER_SUPPORT | policy |
| POL-SP-010-Acceptable Use of Assets Vietnam.pdf | .pdf | ok | 12 | 12 | 23456 | 0 | ELCA_GENERAL | policy |
| Referral Bonus Scheme 2025.pdf | .pdf | ok | 5 | 5 | 6654 | 0 | ELCA_HR | policy |
| Referral Bonus Scheme.pdf | .pdf | ok | 5 | 5 | 6721 | 0 | ELCA_HR | policy |
| Referral_Program.pptx | .pptx | ok | 5 | 5 | 2848 | 0 | ELCA_HR | policy |
| SeniorityAnnualLeaveGrant.pdf | .pdf | ok | 0 | 0 | 0 | 4 | ELCA_HR | policy |
| SỔ TAY KHẨN CẤP - CII TOWER (final).pdf | .pdf | ok | 22 | 22 | 32067 | 2 | CII_TOWER_SUPPORT | guidance |
| SỔ TAY TOÀ NHÀ - CII TOWER (final).pdf | .pdf | ok | 28 | 28 | 59176 | 2 | CII_TOWER_SUPPORT | guidance |
| WorkFromHome_Policy_Regulations.pdf | .pdf | ok | 0 | 0 | 0 | 3 | ELCA_HR | policy |
| WorkingDaysAndWorkingHours_Regulations.pdf | .pdf | ok | 0 | 0 | 0 | 5 | ELCA_HR | policy |

## Reviewed Source Map

| File | Usage | Canonical group | Notes |
|---|---|---|---|
| AcceptableUseOfAssetsVN_EN.pdf | superseded_scanned_variant | acceptable-use-of-assets | Image-only bilingual variant. Use POL-SP-010 for grounded text questions. |
| Attribution of additional annual leave days with seniority.pdf | canonical_policy | annual-leave-seniority | Searchable bilingual scheme. Use for annual leave and seniority entitlement questions. |
| CII Regulation - Building Handbook (Eng).pdf | canonical_guidance | cii-building-handbook | Searchable English building handbook with image candidates. Use for CII factual and emergency-flowchart questions. |
| CII Tower No-Smoking Policy Reminders(Vietnamese).pdf | image_only_supporting | cii-no-smoking-reminder | Unique Vietnamese image-only reminder. Include an image-surfacing case after caption verification. |
| Code of Conduct [EN].pdf | superseded_scanned_variant | code-of-conduct | Image-only English variant. Use POL-LE-005 for grounded text questions. |
| ELCA Vietnam Offices – Bicycle Parking [VN-EN].pdf | image_only_supporting | bicycle-parking | Unique bilingual image-only office notice. Include an image-surfacing case after caption verification. |
| ELCA VN_Oracle_Guidelines for newcomers.pdf | canonical_guidance | oracle-newcomer-guidance | Searchable Oracle onboarding instructions with screenshots. Use for procedure and screenshot-surfacing cases. |
| EmployeeNonDisclosureAgreement.doc | canonical_policy | employee-nda | Legacy DOC now extracts after isolated LibreOffice profile fix. |
| Employees Laptop Buy- Back Scheme.pdf | canonical_policy | laptop-buyback | Searchable scheme. Prefer this file over the scanned lowercase variant. |
| How to Refer a Candidate on Oracle.docx | canonical_guidance | candidate-referral-oracle | Searchable short Oracle referral procedure. |
| Internal purchasing form.xlsx | form_template | internal-purchasing-form | Use for form surfacing, not general policy answers. |
| InternalLabor_Regulation_VN_EN.pdf | superseded_scanned_variant | internal-labour-regulations | Image-only bilingual variant. Use POL-HR-011 for grounded text questions. |
| laptop buy-back regulation.pdf | superseded_scanned_variant | laptop-buyback | Image-only variant. Use Employees Laptop Buy- Back Scheme.pdf for grounded text questions. |
| MissionForm_OnCallSupport.xlsx | form_template | on-call-support-form | Use for on-call mission form download checks. |
| MissionForm_ProductionMonitoring.xlsx | form_template | production-monitoring-form | Use for production monitoring form download checks. |
| OnCallSupport_Regulation.pdf | superseded_scanned_variant | on-call-support | Image-only variant. Use POL-HR-014 for grounded text questions. |
| Overtime Form.xlsx | form_template | overtime-form | Use for overtime form download checks. |
| Overtime request & actual 2024.xlsx | historical_form_data | overtime-request-actual | Historical workbook. Avoid employee-specific factual questions; use only ingestion and retrieval boundary tests. |
| Payment process V2_2023.pdf | canonical_policy | payment-process | Searchable bilingual process. Use for payment requirements and procedure questions. |
| Payment request form.docx | form_template | payment-request-form | Use for payment request form surfacing checks. |
| PIT depedent register (Form 07.XN.NPT.TNCN) - Confirmation of direct nurturing of dependent.docx | supporting_form | pit-dependent-registration | Supporting dependent-registration form. |
| PIT depedent register (Guidance 2024).docx | superseded_guidance | pit-dependent-registration | 2024 guidance duplicate representation. Prefer 2026 PDF for current-answer questions. |
| PIT depedent register (Guidance 2024).pdf | superseded_guidance | pit-dependent-registration | 2024 guidance. Use only for explicit version-aware comparisons. |
| PIT depedent register (Guidance 2026).pdf | canonical_guidance | pit-dependent-registration | Current guidance. Prefer for dependent-registration questions. |
| PIT dependent register - Power of attorney for registration of dependent.doc | parser_error_supporting_form | pit-dependent-registration | Legacy DOC does not convert with LibreOffice. Keep in audit; exclude from question generation until manually replaced or repaired. |
| PIT Dependent register 2025 (Form 20-ĐK-TCT) - Form đăng ký.docx | canonical_form | pit-dependent-registration | Current dependent-registration declaration form. |
| PIT dependent regiter (Form 07.ĐK.NPT.TNCN).doc | parser_error_superseded_form | pit-dependent-registration | Legacy DOC does not convert with LibreOffice and is superseded by the current Form 20 DOCX. |
| PITDependentRegister_Form20DKTCT.docx | supporting_form | pit-dependent-registration | Alternate Form 20 representation. Use only for duplicate and form-surfacing checks. |
| PITDependentRegister_Guidance.pdf | legacy_guidance | pit-dependent-registration | Older dependent-registration guidance. Use only for version-aware testing. |
| POL-HR-011-Internal Labour Regulations Vietnam.pdf | canonical_policy | internal-labour-regulations | Searchable current internal labour regulation. Major HR question source. |
| POL-HR-012-Working Days and Hours Vietnam.pdf | canonical_policy | working-days-hours | Searchable current working-days and hours policy. |
| POL-HR-013-Work-From-Home Policy Vietnam.pdf | canonical_policy | work-from-home | Searchable current work-from-home policy. |
| POL-HR-014-On-call Support Regulation Vietnam.pdf | canonical_policy | on-call-support | Searchable current on-call and production-monitoring regulation. |
| POL-LE-005-Code of Conduct Vietnam.pdf | canonical_policy | code-of-conduct | Searchable code-of-conduct policy. |
| POL-LE-006-Office Building Regulations Vietnam.pdf | canonical_policy | office-building-regulations | Searchable office-building policy based on the CII handbook. |
| POL-SP-010-Acceptable Use of Assets Vietnam.pdf | canonical_policy | acceptable-use-of-assets | Searchable acceptable-use policy. |
| Referral Bonus Scheme 2025.pdf | superseded_policy | referral-bonus | Version 1.2 released in 2025. Use only for explicit version-aware comparisons. |
| Referral Bonus Scheme.pdf | canonical_policy | referral-bonus | Version 1.3 released in 2026. Prefer for current referral-bonus questions. |
| Referral_Program.pptx | legacy_guidance | referral-program | Legacy referral slide deck. Extracts after isolated LibreOffice profile fix. |
| SeniorityAnnualLeaveGrant.pdf | superseded_scanned_variant | annual-leave-seniority | Image-only variant. Use Attribution of additional annual leave days with seniority.pdf for grounded text questions. |
| SỔ TAY KHẨN CẤP - CII TOWER (final).pdf | canonical_guidance | cii-emergency-handbook | Searchable Vietnamese emergency handbook with flowchart image candidates. |
| SỔ TAY TOÀ NHÀ - CII TOWER (final).pdf | canonical_guidance | cii-building-handbook | Searchable Vietnamese building handbook. |
| WorkFromHome_Policy_Regulations.pdf | superseded_scanned_variant | work-from-home | Image-only variant. Use POL-HR-013 for grounded text questions. |
| WorkingDaysAndWorkingHours_Regulations.pdf | superseded_scanned_variant | working-days-hours | Image-only variant. Use POL-HR-012 for grounded text questions. |

## Review Checklist

- Confirm domain assignments.
- Confirm policy, guidance, and form roles.
- Group duplicate, translated, superseded, and canonical versions.
- Inspect extraction errors and empty-text documents.
- Mark image-heavy files that require image-surfacing questions.
- Select source excerpts before generating evaluation questions.
