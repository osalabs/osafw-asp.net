make_table_list(".list")

$('#FFilter').find('select, input[type="radio"], input[type="checkbox"]').on('change', function(){
    $('#FFilter').trigger('submit');
});

var $upload = $('#FQuickUpload');
$upload.find('input[type=file]').on('change', function(e){
    $upload.find('.wait-msg').html(HTML_LOADING);    
    $upload.submit();
});