window.articleEditor = {
    init: function (textArea, initialHtml) {
        if (!textArea) return;

        const hasJQuery = typeof window.jQuery !== "undefined";
        const hasSummernote = hasJQuery && window.jQuery.fn && window.jQuery.fn.summernote;

        // Fallback simple si Summernote absent
        if (!hasSummernote) {
            textArea.value = initialHtml || "";
            return;
        }

        const $el = window.jQuery(textArea);

        // Déjà initialisé -> on met juste le contenu
        if ($el.data("summernote")) {
            $el.summernote("code", initialHtml || "");
            return;
        }

        $el.summernote({
            placeholder: "Écrivez votre article...",
            height: 300,
            dialogsInBody: true,
            styleTags: ["p", "h2", "h3", "blockquote", "pre"],
            toolbar: [
                ["style", ["style"]],
                ["font", ["bold", "italic", "underline", "clear"]],
                ["fontname", ["fontname"]],
                ["fontsize", ["fontsize"]],
                ["color", ["color"]],
                ["para", ["ul", "ol", "paragraph"]],
                ["insert", ["link", "picture"]], // <- important
                ["view", ["codeview"]],
            ],
            fontNames: ["Arial", "Arial Black", "Courier New", "Roboto", "Times New Roman"],
            fontSizes: ["8", "10", "12", "14", "16", "18", "20", "24", "28", "32", "36"],
        });

        $el.summernote("code", initialHtml || "");
    },

    getHtml: function (textArea) {
        if (!textArea) return "";

        const hasJQuery = typeof window.jQuery !== "undefined";
        const hasSummernote = hasJQuery && window.jQuery.fn && window.jQuery.fn.summernote;

        if (hasSummernote) {
            const $el = window.jQuery(textArea);
            if ($el.data("summernote")) {
                return $el.summernote("code") || "";
            }
        }

        // Fallback : textarea standard
        return textArea.value || "";
    },

    insertImage: function (textArea, url) {
        if (!textArea || !url) return;

        const hasJQuery = typeof window.jQuery !== "undefined";
        const hasSummernote = hasJQuery && window.jQuery.fn && window.jQuery.fn.summernote;

        if (hasSummernote) {
            const $el = window.jQuery(textArea);
            if ($el.data("summernote")) {
                $el.summernote("insertImage", url);
                return;
            }
        }

        // Fallback : insertion HTML dans le textarea (simple)
        const tag = `<img src="${url}" alt="" />`;
        const start = textArea.selectionStart ?? textArea.value.length;
        const end = textArea.selectionEnd ?? textArea.value.length;
        const before = textArea.value.substring(0, start);
        const after = textArea.value.substring(end);
        textArea.value = before + tag + after;
    }
};
