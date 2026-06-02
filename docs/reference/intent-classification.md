# Intent Classification Reference

## Intents

| Intent | Meaning |
|---|---|
| `SMALLTALK` | Greetings, thanks, casual chat |
| `POLICY_QUERY` | Company policy, HR, IT, finance, facilities, procedures, forms |
| `OUT_OF_SCOPE` | Not related to company policies |

Default unexpected classifier result to `POLICY_QUERY`.

## Fast path

Run before LLM call.

### SMALLTALK keywords

English:

- hi
- hello
- hey
- thanks
- thank you

Vietnamese:

- chào
- xin chào
- cảm ơn

French:

- bonjour
- salut
- merci

German:

- hallo
- guten tag
- danke

Also classify message length `< 10` as `SMALLTALK` unless it contains policy keyword.

### POLICY_QUERY keywords

English:

- policy
- leave
- salary
- benefits
- overtime
- recruitment
- security
- IT
- finance
- facility
- form

Vietnamese:

- chính sách
- nghỉ phép
- lương
- phúc lợi
- tăng ca
- tuyển dụng
- bảo mật
- cơ sở vật chất
- biểu mẫu

French:

- politique
- congé
- salaire
- avantages
- heures supplémentaires
- recrutement
- sécurité
- formulaire

German:

- richtlinie
- urlaub
- gehalt
- leistungen
- überstunden
- rekrutierung
- sicherheit
- formular

## LLM fallback prompt

```text
Classify the user message into exactly one category:
- SMALLTALK: greetings, thanks, casual conversation
- POLICY_QUERY: any question about company policies, HR, leave, overtime, salary, benefits, IT, facilities, procedures, forms, CII Tower
- OUT_OF_SCOPE: questions unrelated to company policies

When in doubt between POLICY_QUERY and OUT_OF_SCOPE, choose POLICY_QUERY.
Respond with ONLY the category name.

User message: {message}
```

Use `maxTokens: 10`.

## Static response templates

### Smalltalk

`en`: Hello. Ask me any question about ELCA company policies.

`vi`: Xin chào. Bạn có thể hỏi tôi về các chính sách của công ty ELCA.

`fr`: Bonjour. Vous pouvez me poser des questions sur les politiques de l'entreprise ELCA.

`de`: Hallo. Sie können mir Fragen zu den Unternehmensrichtlinien von ELCA stellen.

### Out of scope

`en`: I can only answer questions about ELCA company policies.

`vi`: Tôi chỉ có thể trả lời các câu hỏi về chính sách của công ty ELCA.

`fr`: Je peux uniquement répondre aux questions sur les politiques de l'entreprise ELCA.

`de`: Ich kann nur Fragen zu den Unternehmensrichtlinien von ELCA beantworten.

### No results

`en`: I could not find relevant policy information in the uploaded documents.

`vi`: Tôi không tìm thấy thông tin chính sách liên quan trong các tài liệu đã tải lên.

`fr`: Je n'ai pas trouvé d'informations pertinentes dans les documents importés.

`de`: Ich konnte in den hochgeladenen Dokumenten keine relevanten Richtlinieninformationen finden.

## Router agent categories

Phase 6 only:

- `ELCA_HR`
- `ELCA_GENERAL`
- `CII_TOWER_SUPPORT`

Unexpected router result defaults to `ELCA_GENERAL`.
