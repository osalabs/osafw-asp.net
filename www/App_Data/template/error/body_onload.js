$(document).on('click', '.on-dropinfo', function (e) {
    var $this = $(this);
    var $alert = $this.closest('.dropinfo').find('.alert');
    if ( $alert.is(':visible') ){
        $alert.hide();
        $this.find('.glyphicon').removeClass('glyphicon-menu-up').addClass('glyphicon-menu-down');
    }else{
        $alert.show();
        $this.find('.glyphicon').removeClass('glyphicon-menu-down').addClass('glyphicon-menu-up');
    }
});