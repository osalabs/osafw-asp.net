$(document).on('click', '.on-all-cb', function (e) {
    var $this = $(this);
    var cbclass=$this.data('cb');
    $(cbclass).prop('checked', $this.prop('checked'));
});