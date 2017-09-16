function pwd_hideshow(){
 var chpwd;
 if ( $('#chpwd')[0].checked ){
    $('#pwd').show().val( $('#pwdh').val() );
    $('#pwdh').hide();
 }else{
    $('#pwd').hide();
    $('#pwdh').show().val( $('#pwd').val() );
 }
}
