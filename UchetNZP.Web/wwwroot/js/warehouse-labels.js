(function ($) {
    function toggleLabels(button) {
        var $button = $(button);
        var targetSelector = $button.data('target');
        if (!targetSelector) {
            return;
        }

        var $target = $(targetSelector);
        if ($target.length === 0) {
            return;
        }

        var isHidden = $target.hasClass('d-none');

        $('.warehouse-labels-row').not($target).each(function () {
            $(this).addClass('d-none');
        });
        $('.js-warehouse-labels-toggle').not($button).attr('aria-expanded', 'false');

        if (isHidden) {
            $target.removeClass('d-none');
            $button.attr('aria-expanded', 'true');
        } else {
            $target.addClass('d-none');
            $button.attr('aria-expanded', 'false');
        }
    }

    $(document).on('click', '.js-warehouse-labels-toggle', function (event) {
        event.preventDefault();
        toggleLabels(this);
    });
})(jQuery);
