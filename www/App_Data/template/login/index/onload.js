$(document).on('click', '.on-pwd-hideshow', pwd_hideshow);
pwd_hideshow();

//$('#login').focus();

$(document).on('keyup change', '.form-label-group input', input_on_change);
$('.form-label-group input').trigger('change');


function input_on_change(e) {
  var $this=$(this);
  if ($this.val()>''){
      if (!$this.is('.filled')) $this.addClass('filled');
  }else{
      $this.removeClass('filled');
  }
}

function pwd_hideshow(){
  var chpwd;
  if ( $('#chpwd')[0].checked ){
    $('#pwdh').hide();
    $('#pwd').show().val( $('#pwdh').val() ).trigger('change');
  }else{
    $('#pwd').hide();
    $('#pwdh').show().val( $('#pwd').val() ).trigger('change');
  }
}
