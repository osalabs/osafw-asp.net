$(document).on('click', '.on-pwd-hideshow', pwd_hideshow);
pwd_hideshow();

// $('#login').focus()

$(document).on('keyup change', '.form-label-group input', input_on_change);
$('.form-label-group input').trigger('change');


function input_on_change(e) {
  var $this=$(this);
  var is_filled= ($this.val()>'');
  if (!is_filled){
    //detect browser autofill
    //try/catch for non-webkit
    try {
      is_filled = $this.is(":-webkit-autofill")
    }catch(e){
      // console.log(e)
    }
  }

  if (is_filled){
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
