/*
    EduSathi - shared frontend behaviour.

    Collected from the inline <script> blocks that lived in the Web Forms pages
    (PublicSite.Master, Default.aspx, QuestionnaireUpload.aspx, QuestionnaireSolo.aspx,
    QuestionnaireRoom.aspx, Quiz.aspx). Everything here is presentation only - no
    postback, no ClientID lookups. Elements are found by data-* attributes so the
    same code works on every page that opts in.
*/
(function () {
    "use strict";

    /* ---------- Public navbar toggle (was in PublicSite.Master) ---------- */
    function initNavToggle() {
        var toggle = document.getElementById("navToggle");
        var links = document.getElementById("publicNavLinks");
        if (!toggle || !links) return;
        toggle.addEventListener("click", function () {
            links.classList.toggle("open");
        });
    }

    /* ---------- PDF drag & drop zone (was in Default.aspx / QuestionnaireUpload.aspx) ----------
       Markup contract:
         <div class="upload-box" data-dropzone>
           <input type="file" data-dropzone-input />
           <span data-dropzone-filename></span>
           <button data-dropzone-browse>browse</button>
         </div>
    */
    function initDropzones() {
        document.querySelectorAll("[data-dropzone]").forEach(function (zone) {
            var input = zone.querySelector("[data-dropzone-input]");
            var label = zone.querySelector("[data-dropzone-filename]");
            var browse = zone.querySelector("[data-dropzone-browse]");
            if (!input) return;

            if (browse) {
                browse.addEventListener("click", function () { input.click(); });
            }

            function showName() {
                if (label) {
                    label.textContent = input.files && input.files.length ? input.files[0].name : "";
                }
            }

            ["dragenter", "dragover"].forEach(function (evt) {
                zone.addEventListener(evt, function (e) {
                    e.preventDefault();
                    zone.classList.add("dragover");
                });
            });
            ["dragleave", "drop"].forEach(function (evt) {
                zone.addEventListener(evt, function (e) {
                    e.preventDefault();
                    zone.classList.remove("dragover");
                });
            });
            zone.addEventListener("drop", function (e) {
                if (e.dataTransfer.files.length) {
                    input.files = e.dataTransfer.files;
                    showName();
                }
            });
            input.addEventListener("change", showName);
        });
    }

    /* ---------- Pill pickers (was pickPill() in QuestionnaireSolo.aspx) ----------
       Markup contract:
         <div class="pill-group" data-pill-group="Difficulty">
           <button type="button" class="pill-btn selected" data-value="Medium">Medium</button>
         </div>
         <input type="hidden" name="Difficulty" data-pill-target="Difficulty" value="Medium" />
    */
    function initPillGroups() {
        document.querySelectorAll("[data-pill-group]").forEach(function (group) {
            var name = group.getAttribute("data-pill-group");
            var target = document.querySelector('[data-pill-target="' + name + '"]');
            group.querySelectorAll(".pill-btn").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    group.querySelectorAll(".pill-btn").forEach(function (b) {
                        b.classList.remove("selected");
                    });
                    btn.classList.add("selected");
                    if (target) target.value = btn.getAttribute("data-value");
                });
            });
        });
    }

    /* ---------- Copy-to-clipboard (was copyCode() in QuestionnaireRoom.aspx) ---------- */
    function initCopyButtons() {
        document.querySelectorAll("[data-copy-target]").forEach(function (btn) {
            btn.addEventListener("click", function (e) {
                e.preventDefault();
                var source = document.getElementById(btn.getAttribute("data-copy-target"));
                if (!source) return;
                var text = source.innerText.replace(/\s+/g, "");
                if (navigator.clipboard) {
                    navigator.clipboard.writeText(text);
                }
            });
        });
    }

    /* ---------- Live "n/N answered" counter (was in Quiz.aspx) ---------- */
    function initAnsweredCounter() {
        var countEl = document.querySelector("[data-answered-count]");
        if (!countEl) return;
        var lists = document.querySelectorAll(".option-list ol");
        var total = lists.length;

        function refresh() {
            var answered = 0;
            lists.forEach(function (ol) {
                if (ol.querySelector('input[type="radio"]:checked')) answered++;
            });
            countEl.textContent = answered + "/" + total + " answered";
        }

        lists.forEach(function (ol) { ol.addEventListener("change", refresh); });
        refresh();
    }

    /* ---------- Processing screen step animation (was in Processing.aspx) ----------
       TODO(backend): replace the timer with real progress events from the Gemini
       pipeline (poll an endpoint, or push over SignalR), then navigate to the URL
       in data-processing-next only when the work has actually finished.
    */
    function initProcessing() {
        var host = document.querySelector("[data-processing]");
        if (!host) return;
        var steps = host.querySelectorAll(".proc-step");
        var bar = host.querySelector("[data-processing-bar]");
        var next = host.getAttribute("data-processing-next");
        var i = 0;

        function tick() {
            if (i > 0) steps[i - 1].classList.add("done");
            if (i < steps.length) {
                steps[i].classList.add("active");
                if (bar) bar.style.width = Math.round(((i + 1) / steps.length) * 100) + "%";
                i++;
                setTimeout(tick, 900);
            } else if (next) {
                setTimeout(function () { window.location.href = next; }, 500);
            }
        }
        tick();
    }

    function init() {
        initNavToggle();
        initDropzones();
        initPillGroups();
        initCopyButtons();
        initAnsweredCounter();
        initProcessing();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
