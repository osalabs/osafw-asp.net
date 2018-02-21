var id = parseInt('<~id>',10);

$(document).on('click', '.on-send-pwd', on_send_pwd);
$(document).on('click', '.on-change-pwd', on_change_pwd);

if (!id){
    //workaround for Chrome autofill
    setTimeout(function (e) {
        $('#pwd').removeClass('d-none');
    },200);
}

function on_send_pwd(e) {
    $.getJSON('<~../url>/(SendPwd)/<~id>', function(data){
        if (data.success){
            fw.ok('Password reminder email sent');
        }else{
            fw.error('Server error occured');
        }
    });
}

function on_change_pwd (e) {
    $(this).hide();
    $('#pwd').removeClass('d-none').focus();
}