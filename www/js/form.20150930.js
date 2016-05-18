var WAIT_HTML = '<div class="wait-html"><i class="ico-wait"></i> Loading...</div>';
var HTML_LOADING='<img src="'+ROOT_URL+'/img/loading.gif"> Loading...';

function ajaxify_list_navigation(div_nav, onclick){
 $('a',div_nav).click(function(){
    onclick(this);
    return false;
 });
}

function hint_ok(str){
 $.jGrowl(str, {theme: 'hint_green'});
}

function hint_error(str){
 $.jGrowl(str, {theme: 'hint_error'});
}

//for all forms with data-check-changes on a page - setup changes tracker, call in $(document).ready()
function setup_cancel_form_handlers() {
  $('body').on('change', 'form[data-check-changes]', function(){
    $(this).data('is-changed', true);
  });
  $('body').on('submit', 'form[data-check-changes]', function(){
    //on form submit - disable check for
    $(this).data('is-changed-submit', true);
  });
  $(window).on('beforeunload', function (e) {
    var is_changed = false;
    $('form[data-check-changes]').each(function(index, el) {
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
}

//check if form changed
// if yes - confirm before going to other url
// if no - go to url
function cancel_form(F, url){
  var $F=$(F);
  var is_cancel=true;

  if ( $F.data('is-changed')===true ){
    is_cancel=confirm('There are unsaved changes.\nDo you really want to Cancel editing the Form?');
  }
  if (is_cancel){
    window.location=url;
  }
  return false;
}

function delete_btn(ael){
  if (confirm('ARE YOU SURE you want to delete this item?')) {
    var XSS=$(ael).parents('form:first').find("input[name=XSS]").val();
    var action = ael.href+ ( ( ael.href.match(/\?/) ) ? '&':'?' ) +'XSS='+XSS;
    $('#FOneDelete').attr('action', action).submit();
  }
  return false;
}

// depends on #FFilter
function make_table_list(tbl){
  var jtbl=$(tbl);
  // $("tbody tr:even", tbl).addClass('even');
  // $("tbody tr:odd", tbl).addClass('odd');
  $("tbody tr", tbl).hover(function(){
    $(this).addClass("hover");
  },function(){
    $(this).removeClass("hover");
  })
  .dblclick(function(){
    if ($(this).data('url')) window.location=$(this).data('url');
  });

  var rowtitle=jtbl.data('rowtitle');
  if (!rowtitle) rowtitle='Double click to Edit';
  var title_selector = "tbody tr";
  if (jtbl.data('rowtitle-type')=='explicit') title_selector="tbody tr td.rowtitle";
  $(title_selector, tbl).attr('title', rowtitle);

  var $sh=$(".sort-header", tbl);
  var sortby=$sh.data('sortby');
  var sortdir=$sh.data('sortdir');

  $(".sortable", tbl).each(function() {
    var $td=$(this);
    if ( $td.data('sort')==sortby ){
      var sort_img= (sortdir=='desc') ? 'glyphicon-arrow-up' : 'glyphicon-arrow-down';
      $td.addClass('active-sort').prepend('<span class="glyphicon '+sort_img+' pull-right"></span>');
    }
  }).on('click', function() {
    var $td=$(this);
    var $f=$('#FFilter');
    var sortdir=$(".sort-header", tbl).data('sortdir');
    //console.log(sortdir, $td.is('.active'));

    if ( $td.is('.active-sort') ){
      //active column - change sort dir
      sortdir = (sortdir=='desc') ? 'asc' : 'desc';
    }else{
      //new collumn - set sortdir to default
      sortdir='asc';
    }

    $('input[name="f[sortdir]"]', $f).val(sortdir);
    $('input[name="f[sortby]"]', $f).val( $td.data('sort') );

    $f.submit();
  });

  //make table header freeze if scrolled to far below
    $(window).bind('resize, scroll', function (e) {
        //debounce
        clearTimeout(window.to_scrollable);
        window.to_scrollable=setTimeout(function (e) {
            apply_scrollable_table(jtbl);
        }, 10);
    });

}

//make table in pane scrollable with fixed header
//if scrollable header exists - just recalc positions (i.e. if called from window resize)
function apply_scrollable_table($table, is_force) {
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
        $table.find('thead').find('tr:first').css({visibility: ''});;
        return;
    }

    if (!$dh.length || is_force){
        $dh.remove();

        //create fixed header for the table
        var $th_orig = $table.find('thead');
        $th_orig.find('tr:first').css({visibility: 'hidden'});

        var $th = $table.find('thead').clone(true);
        $th.find('tr').not(':eq(0)').remove(); //leave just first tr
        $th.find('tr:first').css({visibility: ''});

        var $htable = $('<table></table>').width($table.width()).append( $th );
        $htable[0].className = $table[0].className; //apply all classes
        $htable.removeClass('data-table');

        var $th0 = $table.find('thead > tr:first > th');
        var $thh = $htable.find('thead > tr:first > th');
        $th0.each(function(i,el) {
            $thh.eq(i).outerWidth( $(this).outerWidth() );
        });

        var $dh = $('<div class="data-header"></div>').append($htable).css({
            top: 0,
            left: to.left-window.scrollX
        });

        $scrollable.append( $dh );

    }else{
        //just adjust the header position
        $dh.css({
            left: to.left-window.scrollX
        });
    }
}


function list_chkall(cab){
 $(".multicb", cab.form).prop('checked', cab.checked);
}

// <a href="#" data-mail-name="NAME" data-mail-domain="DOM" data-mail="subject=XXX">[yyy]</a>
function setup_emails(){
 $('a[data-mail-name]').each(function(index, el) {
    var $el = $(el);
    var name=$el.data('mail-name');
    if (name>''){
        var dom=$el.data('mail-domain');
        if (!dom) dom='baroque.org';

        var more=$el.data('mail');

        el.href='mailto:'+name+'@'+dom+((more>'')?'?'+more:'');
        if ( $el.text()=='' ){
            $el.text(name+'@'+dom);
        }
    }else{
        $el.text('');
    }
 });
}

function title2url(from_input, to_input){
    var title=$(from_input).val();
    title=title.toLowerCase();
    title=title.replace(/^\W+/,'');
    title=title.replace(/\W+$/,'');
    title=title.replace(/\W+/g,'-');
    $(to_input).val(title);
}

function field_insert_at_cursor(myField, myValue) {
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
}

/* usage:
$(document).on('click', '.on-share', toggle_share);
$(document).on('click', '#sharer a', click_share);
 */
function toggle_share (e) {
    e.preventDefault();
    var el = this;
    var $this = $(this);
    $sharer = $('#sharer');

    $sharer.remove();//close any opened

    if (!$sharer.length || $sharer[0].el!=el){
        //this share opened by another element, so show share again on this element

        var $concert = $this.closest('.sb-content-concert');
        var config = {
            url: $concert.data('url'),
            title: $concert.data('iname')
        };

        var os = $this.offset();
        var $sharer=$('<div id="sharer">'
                +'<a href="#" data-share="facebook"><i class="ico-share ico-facebook"></i></a>'
                +'<a href="#" data-share="pinterest"><i class="ico-share ico-pinterest"></i></a>'
                +'<a href="#" data-share="twitter"><i class="ico-share ico-twitter"></i></a>'
                +'<a href="#" data-share="linkedin"><i class="ico-share ico-linkedin"></i></a>'
                +'<a href="#" data-share="mail"><i class="ico-share ico-mail"></i></a>'
                +'</div>');
        $(document.body).append($sharer);
        $sharer.css('top',os.top+7).css('left', os.left-$sharer.width()-12);
        $sharer[0].el = el;
        $sharer[0].config = config;
    }
}
function click_share (e) {
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
}