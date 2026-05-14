# Workflow Fix Bug Triệt Để — PhotoBooth Stability Epic

## Tổng quan

Workflow này dành cho việc fix từng story trong Epic **"Sửa Lỗi & Cải thiện Độ ổn định"**. Mỗi story đi qua **5 vòng kiểm duyệt** để đảm bảo lỗi được sửa triệt để, không phát sinh lỗi mới, và code đạt chất lượng production.

> ⚠️ **Quan trọng:** Mỗi bước đánh dấu "New LLM" nghĩa là bắt đầu một cuộc hội thoại MỚI để tránh context cũ làm nhiễu.

---

## Thứ tự Story cần fix

```
1. story-1-api-security          🔴 (có thể làm song song với Story 2)
2. story-2-bitmap-memory-leak    🔴 (có thể làm song song với Story 1)
3. story-3-camera-stability      🔴 (phụ thuộc Story 2)
4. story-4-network-consolidation 🟠
5. story-5-session-cleanup       🟠
6. story-6-code-quality-cleanup  🟡
```

---

## Workflow cho MỖI Story

### Bước 1: Chuẩn bị & Nâng cấp Story _(New LLM)_

```
/create-story <tên story>
```

**Mục đích:** Đảm bảo story đã đầy đủ Acceptance Criteria, Tasks, và các edge case.

**Input:** File story gốc (VD: `story-1-api-security.md`)

**Việc cần làm:**
1. Yêu cầu agent đọc file story hiện tại
2. Agent rà soát và bổ sung các thiếu sót:
   - Acceptance Criteria có đủ rõ ràng để verify không?
   - Tasks có bỏ sót file nào không?
   - Edge cases nào chưa được xét? (VD: race condition, rollback)
3. **Validate story:** Đọc lại story đã cập nhật, kiểm tra logic
4. **Fix all:** Sửa cho đến khi story hoàn chỉnh

**Output:** Story file được cập nhật, trạng thái chuyển sang `ready-for-dev`

---

### Bước 2: Party-mode Review Story _(New LLM)_

```
/party-mode review story <tên story>
```

**Mục đích:** Nhiều agent (Architect, QA, Dev, PM) cùng phản biện story từ nhiều góc nhìn.

**Việc cần làm:**
1. Agent Architect kiểm tra: giải pháp có phù hợp kiến trúc không?
2. Agent QA kiểm tra: acceptance criteria có đủ testable không?
3. Agent Dev kiểm tra: tasks có khả thi, có side effect gì không?
4. Agent PM kiểm tra: scope có hợp lý, có bị scope creep không?
5. **Fix all findings:** Sửa tất cả vấn đề được phát hiện

**Output:** Story file đã qua review đa chiều, sẵn sàng implement

---

### Bước 3: Implement Story _(New LLM)_

```
/dev-story <tên story>
```

**Mục đích:** Thực hiện coding theo từng task trong story.

**Việc cần làm:**
1. Agent đọc story file, hiểu rõ scope và acceptance criteria
2. Implement từng task theo thứ tự
3. Sau mỗi task, verify bằng cách:
   - Build thành công (`dotnet build`)
   - Kiểm tra logic bằng mắt
4. Sau khi hoàn thành tất cả tasks:
   - Chạy build toàn bộ solution
   - Đánh dấu acceptance criteria đã hoàn thành
5. Cập nhật story status → `in-review`

**Output:** Code đã được implement, build thành công

---

### Bước 4: Code Review nghiêm ngặt _(New LLM)_

```
/code-review <tên story>
```

**Mục đích:** Review adversarial — tìm tối thiểu 3-10 vấn đề trong code vừa viết.

**Việc cần làm:**
1. Agent đọc story và tất cả file đã thay đổi
2. Kiểm tra từng góc độ:
   - **Correctness:** Logic có đúng không? Edge case?
   - **Security:** Có lỗ hổng mới không? (injection, auth bypass)
   - **Performance:** Có tạo bottleneck mới không?
   - **Memory:** Có rò rỉ resource mới không?
   - **Thread Safety:** Có race condition không?
   - **Error Handling:** Exception có được xử lý đúng không?
3. **Fix all findings:** Sửa tất cả vấn đề, build lại

**Output:** Code đã qua review cứng, không còn vấn đề nghiêm trọng

---

### Bước 5: Party-mode Review Source Code _(New LLM)_

```
/party-mode review source code <tên story>
```

**Mục đích:** Vòng kiểm tra cuối cùng — nhiều agent cùng đọc code thực tế.

**Việc cần làm:**
1. Nhiều agent (Architect, QA, Dev) cùng đọc **code đã sửa** (không phải story)
2. Kiểm tra:
   - Code có tuân thủ kiến trúc hiện tại không? (Architect)
   - Có cần thêm unit test không? (QA)
   - Code style có nhất quán với codebase không? (Dev)
   - Có ảnh hưởng đến các feature khác không? (All)
3. **Fix all findings:** Sửa tất cả vấn đề phát hiện

**Output:** Code hoàn chỉnh, story status → `done`

---

## Checklist tổng hợp cho MỖI Story

```markdown
## Story: [Tên Story]

### Bước 1: Chuẩn bị Story
- [ ] Story đã được /create-story nâng cấp
- [ ] Acceptance Criteria đầy đủ & testable
- [ ] Tasks liệt kê đủ files cần sửa
- [ ] Edge cases đã được xét
- [ ] Status: `ready-for-dev`

### Bước 2: Party Review Story  
- [ ] /party-mode đã review
- [ ] Tất cả findings đã được fix
- [ ] Story vẫn trong scope hợp lý

### Bước 3: Implement
- [ ] /dev-story đã implement tất cả tasks
- [ ] `dotnet build` thành công
- [ ] Tất cả acceptance criteria được đánh dấu
- [ ] Status: `in-review`

### Bước 4: Code Review
- [ ] /code-review tìm và fix tất cả vấn đề
- [ ] Không còn lỗi security, performance, memory
- [ ] Build thành công sau khi fix

### Bước 5: Party Review Code
- [ ] /party-mode review code thực tế
- [ ] Tất cả findings cuối cùng đã fix
- [ ] Code nhất quán với codebase
- [ ] Status: `done` ✅
```

---

## Ví dụ chạy cho Story 1

```
💬 Hội thoại 1:
   /create-story story-1-api-security
   → Review & fix story
   → Story status: ready-for-dev

💬 Hội thoại 2:
   /party-mode review story story-1-api-security
   → Fix all agent findings

💬 Hội thoại 3:
   /dev-story story-1-api-security
   → Implement Task 1.1, 1.2, 1.3
   → dotnet build ✅
   → Story status: in-review

💬 Hội thoại 4:
   /code-review story-1-api-security
   → Tìm 5 vấn đề, fix hết
   → dotnet build ✅

💬 Hội thoại 5:
   /party-mode review source code story-1-api-security
   → Fix 2 findings cuối
   → Story status: done ✅

→ Chuyển sang Story 2...
```

---

## Sau khi hoàn thành TẤT CẢ 6 Stories

1. Build toàn bộ solution: `dotnet build PhotoBooth.slnx`
2. Chạy thử 10 session liên tiếp, theo dõi RAM
3. Test rút cáp camera → auto recovery
4. Test API với và không có token
5. Cập nhật epic status → `done` ✅
