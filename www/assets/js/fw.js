/*
  misc client utils for the osafw framework
  www.osalabs.com/osafw
  (c) 2009-2018 Oleg Savchuk www.osalabs.com
*/

window.fw={
  HTML_LOADING : '<span class="spinner-border" role="status" aria-hidden="true"></span> Loading...',

  ok: function (str, options){
    options = $.extend({}, {theme: 'hint_green'}, options);
    // if (typeof(options)=='undefined') options={theme: 'hint_green'};
    $.jGrowl(str, options);
  },

  error: function (str, options){
    options = $.extend({}, {theme: 'hint_error'}, options);
    // if (typeof(options)=='undefined') options={theme: 'hint_error'};
    $.jGrowl(str, options);
  },

  // usage: fw.alert('Process completed','Worker');
  alert: function (content, title){
    if (!title) title='<i class="glyphicon glyphicon-info-sign"></i> Alert';
    var $modal=$('#fw-modal-alert');
    if (!$modal.length){//add template to document
      $(document.body).append('<div class="modal fade" tabindex="-1" role="dialog" id="fw-modal-alert"><div class="modal-dialog modal-sm" role="document"><div class="modal-content"><div class="modal-header"><h5 class="modal-title"></h5><button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button></div><div class="modal-body"><p></p></div><div class="modal-footer"><button type="button" class="btn btn-primary btn-block" data-dismiss="modal">OK</button></div></div></div></div>');
      $modal=$('#fw-modal-alert');
    }
    $modal.modal('show').find('.modal-title').html(title).end().find('.modal-body p').html(content);
    $modal.off('shown.bs.modal').on('shown.bs.modal', function (e) {
      $modal.find('.btn-primary').focus();
    });
  },

  /*
  fw.confirm('Are you sure?', 'optional title', function(){
    //proceed OK answer
  });
  */
  confirm: function (content, title_or_cb, callback){
    if ($.isFunction(title_or_cb)){
      title='';
      callback=title_or_cb;
    }else{
      title=title_or_cb;
    }
    if (!title) title='<i class="glyphicon glyphicon-question-sign"></i> Confirm';
    var $modal=$('#fw-modal-confirm');
    if (!$modal.length){//add template to document
      $(document.body).append('<div class="modal fade" tabindex="-1" role="dialog" id="fw-modal-confirm"><div class="modal-dialog modal-sm" role="document"><div class="modal-content"><div class="modal-header"><h5 class="modal-title"></h5><button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button></div><div class="modal-body"><p></p></div><div class="modal-footer"><button type="button" class="btn btn-primary" data-dismiss="modal">OK</button><button type="button" class="btn btn-default" data-dismiss="modal">Cancel</button></div></div></div></div>');
      $modal=$('#fw-modal-confirm');
    }
    $modal.modal('show').find('.modal-title').html(title).end().find('.modal-body p').html(content);
    $modal.off('shown.bs.modal').on('shown.bs.modal', function (e) {
      $modal.find('.btn-primary').focus();
    });
    $modal.find('.btn-primary').one('click', function (e) {
      callback();
    });
  },

  //called on document ready
  setup_handlers: function (){
    //list screen init
    fw.make_table_list(".list");

    //list screen init
    var $ffilter = $('form[data-list-filter]:first');

    //advanced search filter
    $(document).on('click', '.on-toggle-search', function (e) {
      var $fis = $ffilter.find('input[name="f[is_search]"]');
      var $el = $('table.list .search');
      if ($el.is(':visible')){
          $el.hide();
          $fis.val('');
      } else {
          $el.show();
          $fis.val('1');
      }
    });

    $(document).on('keyup', 'table.list .search input', function (e) {
      //duplicte input values in the source search row, if it in scrollable 
      var $this = $(this);
      var $table_parent = $this.closest('table.list').parent();
      if ($table_parent.hasClass('data-header')) {
        $table_parent.prev('table.list').find('input[name="' + $this.attr('name') + '"]').val($this.val());
      }
      if (e.which == 13) {// on Enter press
        e.preventDefault();
        //on explicit search - could reset pagenum to 0
        //$ffilter.find('input[name="f[pagenum]"]').val(0);
        $ffilter.trigger('submit');
        return false;
      }
    });

    //on filter form submit - add advanced search fields into form
    $ffilter.on('submit', function (e) {
        var $f = $ffilter;
        var $fis = $f.find('input[name="f[is_search]"]');
        if ($fis.val()=='1'){
            //if search ON - add search fields to the form
            $f.find('.osafw-list-search').remove();
            var html=[];
            $('table.list:first .search input').each(function (i, el) {
              if (el.value>''){
                html.push( '<input class="osafw-list-search" type="hidden" name="'+el.name.replace(/"/g,'&quot;')+'" value="'+el.value.replace(/"/g,'&quot;')+'">');
              }
            });
            $f.append(html.join(''));
        }
    });

    //autosubmit filter on change filter fields
    $(document).on('change', 'form[data-list-filter][data-autosubmit] [name^="f["]:input:visible:not([data-nosubmit])', function(){
        this.form.submit();
    });

    //pager click via form filter submit so all filters applied
    $(document).on('click', '.pagination .page-link[data-pagenum]', function (e){
      var $this = $(this);
      var pagenum = $this.data('pagenum');
      var $f = $this.data('filter') ? $($this.data('filter')) : $('form[data-list-filter]:first');
      if ($f){
        e.preventDefault();
        $('<input type="hidden" name="f[pagenum]">').val(pagenum).appendTo($f);
        $f.submit();
      }
    });

    //pagesize change
    $(document).on('change', '.on-pagesize-change', function (e){
      e.preventDefault();
      var $this = $(this);
      var $f = $this.data('filter') ? $($this.data('filter')) : $('form[data-list-filter]:first');
      if ($f){
        $f.find('input[name="f[pagesize]"]').val($this.val());
        $f.submit();
      }
    });

    //list check all/none handler
    $(document).on('click', '.on-list-chkall', function (e){
      $(".multicb", this.form).prop('checked', this.checked);
    });

    //make list multi buttons floating if at least one row checked
    $(document).on('click', '.on-list-chkall, .multicb', function (e) {
      var $bm = $('#list-btn-multi');
      var len = $('.multicb:checked').length;
      if (len>0){
        //float
        $bm.addClass('floating');
        $bm.find('.rows-num').text(len);
      }else{
        //de-float
        $bm.removeClass('floating');
        $bm.find('.rows-num').text('');
      }
    });

    $(document).on('click', '.on-delete-list-row', function (e){
      e.preventDefault();
      fw.delete_btn(this);
    });

    $(document).on('click', '.on-delete-multi', function (e){
      var el=this;
      if (!el._is_confirmed){
        e.preventDefault();
        fw.confirm('Are you sure to delete multiple selected records?', function(){
          el._is_confirmed=true;
          $(el).click();//trigger again after confirmed
        });
      }
    });

    //form screen init
    fw.setup_cancel_form_handlers();
    fw.setup_autosave_form_handlers();
    fw.process_form_errors();
  },

  //for all forms with data-check-changes on a page - setup changes tracker, call in $(document).ready()
  // <form data-check-changes>
  setup_cancel_form_handlers: function() {
    //on cancel buttons handler
    // <a href="url" class="on-cancel">Cancel</a>
    // // <button type="button" data-href="url" class="on-cancel">Cancel</button>
    $(document).on('click', '.on-cancel', function (e) {
      e.preventDefault();
      var $this=$(this);
      var url = $this.prop('href');
      if (!url) url = $this.data('href');
      fw.cancel_form(this.form, url);
    });

    var $forms=$('form[data-check-changes]');
    if (!$forms.length) return; //skip if no forms found

    $('body').on('change', 'form[data-check-changes]', function(){
      $(this).data('is-changed', true);
    });
    $('body').on('submit', 'form[data-check-changes]', function(){
      //on form submit - disable check for
      $(this).data('is-changed-submit', true);
    });
    $(window).on('beforeunload', function (e) {
      var is_changed = false;
      $forms.each(function(index, el) {
          var $form = $(el);
          if ( $form.data('is-changed')===true ){
              if ( $form.data('is-changed-submit')===true ) {
                  //it's a form submit - no need to check this form changes. Only if changes occurs on other forms, we'll ask
              }else{
                  is_changed=true;
                  return false;
              }
          }
      });
      if (is_changed){
          return 'There are unsaved changes.\nDo you really want to Cancel editing the Form?';
      }
    });
  },

  //check if form changed
  // if yes - confirm before going to other url
  // if no - go to url
  cancel_form: function(f, url){
    var $f=$(f);

    if ($f.data('is-ajaxsubmit')===true){
      //if we are in the middle of autosave - wait a bit
      setTimeout(function(){
        fw.cancel_form(f, url);
      },500);
      return false;
    }

    if ( $f.data('is-changed')===true ){
      fw.confirm('<strong>There are unsaved changes.</strong><br>Do you really want to Cancel editing the Form?', function (){
        $f.data('is-changed', false);//force false so beforeunload will pass
        window.location=url;
      });
    }else{
      window.location=url;
    }
    return false;
  },

  //tries to auto save form via ajax on changes
  // '.form-saved-status' element updated with save status
  // <form data-autosave>
  setup_autosave_form_handlers: function () {
    var $asforms=$('form[data-autosave]');
    if (!$asforms.length) return; //skip if no autosave forms found

    //prevent submit if form is in ajax save
    $asforms.on('submit', function (e) {
      var $f = $(this);
      if ($f.data('is-ajaxsubmit')===true){
          //console.log('FORM SUBMIT CANCEL due to autosave in progress');
          e.preventDefault();
          return false;
      }
      $f.data('is-submitting',true); //tell autosave not to trigger
    });

    $('body').on('change', 'form[data-autosave]', function(e){
      var $f = $(this);
      // console.log('on form change', $f, e);
      $f.data('is-changed', true);

      set_status($f, 1);
      // checkboxes trigger autosave instantly
      if ($(e.target).is('input[type=checkbox]:not([data-noautosave])')) $f.trigger('autosave');
    });

    $('body').on('keyup', 'form[data-autosave] :input:not([data-noautosave])', function(e){
      var $inp = $(this);
      //console.log('on keyup');
      if ($inp.data('oldval')!==$inp.val()) {
          var $f = $(this.form);
          $f.data('is-changed', true);
          set_status($f, 1);
      }
    });

    $('body').on('focus', 'form[data-autosave] :input:not([data-noautosave])', function(e){
      var $inp = $(this);
      // console.log('on form input focus');
      $inp.data('oldval', $inp.val());
    });

    // "*:not(.bs-searchbox)" - exclude search input in the bs selectpicker container
    $('body').on('blur', 'form[data-autosave] *:not(.bs-searchbox) > :input:not([data-noautosave])', function(e){
      var $f = $(this.form);
      // console.log('on form input blur', $f);
      if ($f.data('is-changed')===true){
          //form changed, need autosave
          $f.trigger('autosave');
      }
    });

    $('body').on('autosave', 'form[data-autosave]', function (e) {
      var $f = $(this);
      if ($f.data('is-submitting')===true || $f.data('is-ajaxsubmit')===true){
          //console.log('on autosave - ALREADY SUBMITTING');
          //if form already submitting by user intput - schedule autosave again later
          setTimeout(function() {
            $f.trigger('autosave');
          }, 500)
          return false;
      }
      //console.log('on autosave', $f);
      $f.data('is-ajaxsubmit',true);
      var hint_options={};
      if ($f.data('autosave-sticky')){
        hint_options={sticky: true};
      }

      //console.log('before ajaxSubmit', $f);
      set_progress($f, true);
      $f.ajaxSubmit({
          dataType: 'json',
          success: function function_name (data) {
              set_progress($f, false);
              //console.log('ajaxSubmit success', data);
              $('#fw-form-msg').hide();
              fw.clean_form_errors($f);
              if (data.success){
                  $f.data('is-changed', false);
                  set_status($f, 2);
                  if (data.is_new && data.location) {
                      window.location = data.location; //reload screen for new items
                  }else{
                      $f.data('is-ajaxsubmit',false);
                  }
              }else{
                  $f.data('is-ajaxsubmit',false);
                  //auto-save error - highlight errors
                  if (data.ERR) fw.process_form_errors($f, data.ERR);
                  fw.error(data.err_msg ? data.err_msg : 'Auto-save error. Press Save manually.', hint_options);
              }
              if (data.msg) fw.ok(data.msg, hint_options);
              $f.trigger('autosave-success',[data]);
          },
          error: function function_name (argument) {
              set_progress($f, false);
              //console.log('ajaxSubmit error', data);
              $f.data('is-ajaxsubmit',false);
              //hint_error('Server error occured during auto save form');
          }
      });
    });

    function set_status($f, status){
      $f=$($f);
      var $html = $('<span>').append($f[0]._saved_status);
      var spinner = $('<span>').append($html.find('.spinner-border').clone()).html();
      var cls='', txt='';
      if (status==0){
          //nothing
      }else if (status==1){ //not saved
          cls='badge-danger';
          txt='not saved';
      }else if (status==2){ //saved
          cls='badge-success';
          txt='saved';
      }
      var html=spinner+'<span class="badge '+cls+'">'+txt+'</span>';
      $f[0]._saved_status=html;
      $f.find('.form-saved-status').html(html);
      $('.form-saved-status-global').html(html);
    }

    function set_progress($f, is_working){
      $f=$($f);
      var $html = $('<span>').append($f[0]._saved_status);
      var is_spinner = $html.find('.spinner-border').length>0;

      if (is_working){
        if (!is_spinner) $html.prepend('<span class="spinner-border spinner-border-sm mr-2" role="status" aria-hidden="true"></span>');
      }else{
        if (is_spinner) $html.find('.spinner-border').remove();
      }

      $f[0]._saved_status=$html.html();
      $f.find('.form-saved-status').html($html.html());
      $('.form-saved-status-global').html($html.html());
    }

  },

  //cleanup any exisitng form errors
  clean_form_errors: function ($form) {
    $form=$($form);
    $form.find('.has-danger').removeClass('has-danger');
    $form.find('.is-invalid').removeClass('is-invalid');
    $form.find('[class^="err-"]').removeClass('invalid-feedback');
  },

  //form - optional, if set - just this form processed
  //err_json - optional, if set - this error json used instead of form's data-errors
  process_form_errors: function (form, err_json) {
    //console.log(form, err_json);
    var selector= 'form[data-errors]';
    if (form) selector=$(form);
    $(selector).each(function (i, el) {
      var $f = $(el);
      var errors = err_json ? err_json : $f.data('errors');
      if (errors) console.log(errors);
      if ($.isPlainObject(errors)){
        //highlight error fields
        $.each(errors,function(key, errcode) {
          var $input = $f.find('[name="item['+key+']"],[name="'+key+'"]');
          if ($input.length){
            $input.closest('.form-group').not('.noerr').addClass('has-danger'); //highlight whole row (unless .noerr exists)
            $input.addClass('is-invalid'); //mark input itself
            if (errcode!==true && errcode.length){
              var $p=$input.parent();
              if ($p.is('.input-group,.custom-control,.dropdown')) $p = $p.parent();
              $p.find('.err-'+errcode).addClass('invalid-feedback'); //find/show specific error message
            }
          }
        });
      }
    });
  },

  delete_btn: function (ael){
    fw.confirm('<strong>ARE YOU SURE</strong> to delete this item?', function(){
      var XSS=$(ael).parents('form:first').find("input[name=XSS]").val();
      var action = ael.href+ ( ( ael.href.match(/\?/) ) ? '&':'?' ) +'XSS='+XSS;
      $('#FOneDelete').attr('action', action).submit();
    });
    return false;
  },

  // if no data-filter defined, tries to find first form with data-list-filter
  // <table class="list" data-rowtitle="Double click to Edit" [data-rowtitle-type="explicit"] [data-filter="#FFilter"]>
  //  <thead>
  //    <tr data-sortby="" data-sortdir="asc|desc"
  //  ... <tr data-url="url to go on double click">
  //       <td data-rowtitle="override title on particular cell if 'explicit' set above">
  make_table_list: function(tbl){
    var $tbl=$(tbl);
    if (!$tbl.length) return; //skip if no list tables found

    var $f = $tbl.data('filter') ? $($tbl.data('filter')) : $('form[data-list-filter]:first');

    $tbl.on('dblclick', 'tbody tr', function(e){
      var url=$(this).data('url');
      if (url) window.location=url;
    });

    var rowtitle=$tbl.data('rowtitle');
    if (typeof(rowtitle)=='undefined') rowtitle='Double click to Edit';
    var title_selector = "tbody tr";
    if ($tbl.data('rowtitle-type')=='explicit') title_selector="tbody tr td.rowtitle";
    $tbl.find(title_selector).attr('title', rowtitle);

    var $sh=$tbl.find('tr[data-sortby]');
    var sortby=$sh.data('sortby');
    var sortdir=$sh.data('sortdir');

    var sort_img= (sortdir=='desc') ? 'glyphicon-arrow-up' : 'glyphicon-arrow-down';
    $sh.find('th[data-sort="'+sortby+'"]').addClass('active-sort').prepend('<span class="glyphicon '+sort_img+' float-right"></span>');

    $sh.on('click', 'th[data-sort]', function() {
      var $td=$(this);
      var sortdir=$sh.data('sortdir');
      //console.log(sortdir, $td.is('.active'));

      if ( $td.is('.active-sort') ){
        //active column - change sort dir
        sortdir = (sortdir=='desc') ? 'asc' : 'desc';
      }else{
        //new collumn - set sortdir to default
        sortdir='asc';
      }

      if (!$f) return; //skip if no filter form
      $('input[name="f[sortdir]"]', $f).val(sortdir);
      $('input[name="f[sortby]"]', $f).val( $td.data('sort') );

      $f.submit();
    });

    //make table header freeze if scrolled too far below
    $(window).bind('resize, scroll', function (e) {
        //debounce
        clearTimeout(window.to_scrollable);
        window.to_scrollable=setTimeout(function (e) {
            fw.apply_scrollable_table($tbl);
        }, 10);
    });
  },

  //make table in pane scrollable with fixed header
  //if scrollable header exists - just recalc positions (i.e. if called from window resize)
  //requires css  .data-header, .data-header table
  apply_scrollable_table: function ($table, is_force) {
      $table = $($table);
      if (!$table.length) return;//skip if no scrollable table defined

      var $scrollable = $table.closest('.scrollable');
      if (!$scrollable.length) {
          $table.wrap('<div class="scrollable">');
          $scrollable = $table.closest('.scrollable');
      }

      var $dh = $scrollable.find('.data-header');
      var to = $scrollable.offset();
      var win_scrollY= window.pageYOffset || document.documentElement.scrollTop;
      if (win_scrollY<to.top) {
          //no need to show fixed header
          $dh.remove();
          $table.find('thead').css({visibility: ''});
          return;
      }

      if (!$dh.length || is_force){
          $dh.remove();

          //create fixed header for the table
          var $th_orig = $table.find('thead:first');
          $th_orig.css({visibility: 'hidden'});

          var $th = $table.find('thead:first').clone(true);
          //$th.find('tr').not(':eq(0)').remove(); //leave just first tr
          $th.css({visibility: ''});

          var $htable = $('<table></table>').width($table.width()).append( $th );
          $htable[0].className = $table[0].className; //apply all classes
          $htable.removeClass('data-table');

          var $th0 = $table.find('thead:first > tr > th');
          var $thh = $htable.find('thead:first > tr > th');
          $th0.each(function(i,el) {
              $thh.eq(i).outerWidth( $(this).outerWidth() );
          });

          $dh = $('<div class="data-header"></div>').append($htable).css({
              top: 0,
              left: to.left-window.scrollX
          });

          $scrollable.append($dh);

      }else{
          //just adjust the header position
          $dh.css({
              left: to.left-window.scrollX
          });
      }
  },

  //optional, requires explicit call in onload.js
  // <a href="#" data-mail-name="NAME" data-mail-domain="DOM" data-mail="subject=XXX">[yyy]</a>
  setup_mailto: function (){
    $('a[data-mail-name]').each(function(index, el) {
      var $el = $(el);
      var name=$el.data('mail-name');
      if (name>''){
        var dom=$el.data('mail-domain');
        var more=$el.data('mail');

        el.href='mailto:'+name+'@'+dom+((more>'')?'?'+more:'');
        if ( $el.text()==='' ){
            $el.text(name+'@'+dom);
        }
      }else{
        $el.text('');
      }
    });
  },

  title2url: function (from_input, to_input){
      var title=$(from_input).val();
      title=title.toLowerCase();
      title=title.replace(/^\W+/,'');
      title=title.replace(/\W+$/,'');
      title=title.replace(/\W+/g,'-');
      $(to_input).val(title);
  },

  field_insert_at_cursor: function (myField, myValue) {
      //IE support
      if (document.selection) {
          myField.focus();
          sel = document.selection.createRange();
          sel.text = myValue;
      }
      //MOZILLA and others
      else if (myField.selectionStart || myField.selectionStart == '0') {
          var startPos = myField.selectionStart;
          var endPos = myField.selectionEnd;
          myField.value = myField.value.substring(0, startPos) + myValue + myField.value.substring(endPos, myField.value.length);
          myField.selectionStart = startPos + myValue.length; myField.selectionEnd = startPos + myValue.length;
      } else {
          myField.value += myValue;
      }
  },

  /* usage:
  <a href="#" class="on-share" data-url="[optional]" data-title="[optional]" data-top="[optional top offset]" data-left="[optional left offset]">
  $(document).on('click', '.on-share', fw.toggle_share);
   */
  toggle_share: function (e) {
      e.preventDefault();
      var el = this;
      var $this = $(this);
      $sharer = $('#sharer');
      var is_exists = !!$sharer.length;
      if (!is_exists) $(document).on('click', '#sharer a', click_share);

      $sharer.remove();//close any opened

      if (!$sharer.length || $sharer[0].el!=el){
          //this share opened by another element, so show share again on this element
          var data = $this.data();
          var config = {
              url: data.url ? data.url : window.location.href,
              title: data.iname ? data.iname : document.title
          };

          var os = $this.offset();
          var $sharer=$('<div id="sharer">'+
                  '<a href="#" data-share="facebook"><i class="ico-share ico-facebook"></i></a>'+
                  '<a href="#" data-share="pinterest"><i class="ico-share ico-pinterest"></i></a>'+
                  '<a href="#" data-share="twitter"><i class="ico-share ico-twitter"></i></a>'+
                  '<a href="#" data-share="linkedin"><i class="ico-share ico-linkedin"></i></a>'+
                  '<a href="#" data-share="mail"><i class="ico-share ico-mail"></i></a>'+
                  '</div>');
          $(document.body).append($sharer);

          var offset_top = data.top ? data.top : 7;
          var offset_left = data.left ? data.left : -$sharer.width()-12;

          $sharer.css('top',os.top+offset_top).css('left', os.left+offset_left);
          $sharer[0].el = el;
          $sharer[0].config = config;
      }
  },

  click_share: function (e) {
      e.preventDefault();
      var $sharer = $('#sharer');
      var config = $sharer[0].config;
      var to = $(this).data('share');
      var url = encodeURIComponent(config.url);
      var title = encodeURIComponent(config.title);

      var purl;
      if (to=='facebook'){
          purl = 'https://www.facebook.com/sharer/sharer.php?u='+url+'&t='+title;
      }else if (to=='pinterest'){
          purl = 'http://pinterest.com/pin/create/button/?url='+url+'&description='+title;
      }else if (to=='twitter'){
          purl = 'https://twitter.com/intent/tweet?source='+url+'&text='+title;
      }else if (to=='linkedin'){
          purl = 'http://www.linkedin.com/shareArticle?mini=true&url='+url+'&title='+title+'&summary=&source='+url;
      }else if (to=='mail'){
          purl = 'mailto:?subject='+title+'&body='+url;
      }

      if (purl>''){
          var popup_w = 500;
          var popup_h = 350;
          var popup_top  = (screen.height/2) - (popup_h/2);
          var popup_left = (screen.width/2)  - (popup_w/2);
          var options = 'toolbar=no,location=no,status=no,menubar=no,scrollbars=yes,resizable=yes,left='+popup_left+',top='+popup_top+',width='+popup_w+',height='+popup_h;
          window.open(purl, 'share_popup', options);
      }

      $sharer.remove();
  },

  ajaxify_list_navigation: function (div_nav, onclick){
    $(div_nav).on('click', 'a', function(e){
      e.preventDefault();
      onclick(this);
    });
  }

};
