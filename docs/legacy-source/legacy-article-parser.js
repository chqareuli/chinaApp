        function generateLink(match) {
            match = match.replace(new RegExp(" ", "g"), " ");
            match = match.replace(new RegExp("   ", "g"), " ");
            match = match.replace(new RegExp("  ", "g"), " ");

            match = match.replace(new RegExp("\\)", "g"), " \)");
            match = match.replace(new RegExp("<", "g"), " <");
            match = match.replace(new RegExp(";", "g"), " ;");
            /*match = match.replace(new RegExp(",", "g"), " ,");*/
            /*console.log(match);*/
            var result = "";
            var wordsArr = match.split(" ");
            for (var i = 0; i < wordsArr.length; i++) {
                if (wordsArr[i].match(/[\u3400-\u9FBF]/)) {
                    if (match.startsWith('იხ. სურ') || match.startsWith('იხ.სურ'))
                    {
                        result = result + " <span class='dict-see-also'><a href='/get-picture?pictureName=" + wordsArr[i] + "' onclick='openPopup(this.href); return false;'>" + wordsArr[i] + "</a></span>";
                    } else {
                        result = result + " <span class='dict-see-also'><a href='/get-article?word=" + wordsArr[i] + "'>" + wordsArr[i] + "</a></span>";
                        }
                } else {
                    result = result + " " + wordsArr[i];
                }
            }
            result = result.replace(new RegExp("\\s\\)", "g"), "\)");
            result = result.replace(new RegExp("\\s;", "g"), ";");
            result = result.replace(new RegExp("\\s,", "g"), ",");
            return result;
        }

        function insertBreak(match) {
            // console.log("match: " + match);
            if (match.indexOf(">") > -1 && match.indexOf(">):") == -1) {
                return match;
            }

            if (match.indexOf("ხ.") === 0) {
                return match;
            }

            if (match.indexOf("რ.") === 0) {
                return match;
            }

            if (match.indexOf("“") > -1){
                return "<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"+match;
            }
            var arr = match.split(" ");
            console.log(arr);
            console.log(arr[0]+"<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"+arr.slice(1).join(" "));
            var result = arr[0]+"<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"+arr.slice(1).join(" ").trim();
            return result.trim();
        }

        function setBold(match) {
            /*console.log(match);*/

            if (match.indexOf("<br>") > -1 && match.indexOf(":") === -1) {
                return match;
            }

            var char = '〉';
            var i = match.indexOf(char);

/*            if (i === -1) {
                char = ')';
                i = match.indexOf(char);
            }*/

            if (i === -1) {
                char = '】';
                i = match.indexOf(char);
            }
/*
            if (i === -1) {
                var characters = "名s动形s副代量数介连助叹象缀素音";
                for (var j = 0; j<characters.length; j++) {
                    i = match.indexOf(characters[j]);
                    if (i !== -1) {
                        console.log(j);
                        console.log(characters[j]);
                        break;
                    }
                }
            }*/
            // console.log("'"+match+"'");
            var spanIndex = match.indexOf("</span>", i);
            if (spanIndex !== -1) {
                i = spanIndex + "</span>".length;
            }

           /* return match.substring(0,i) + char + "<b>" + match.substring(i+1)+"</b>";*/
            return match.substring(0,i) + "<span class='dict-example'>" + match.substring(i)+"</span>";
        }

        function setItalic(match) {
           /* console.log("setItalic: " + "'"+match+"'");*/

            var char = '〉';
            var endsWith = false;
            var i = match.indexOf(char);
            var spanIndex = match.indexOf("</span>", i);
            if (spanIndex !== -1) {
                i = spanIndex + "</span>".length;
            } else {
                var i1 = match.length;
                if (match[i1] === '<' || match.indexOf(' ') > -1) {
                    endsWith = true;
                    i = match.indexOf(' ');
                    if (match.trim().startsWith('იგივეა, რაც')) {
                        i = match.lastIndexOf(' ');
                    }
                }
            }

            if (endsWith === true) {
                return "<span class='dict-italic'>"+ match.substring(0,i) + "</span>" + match.substring(i);
            }

            var result = match.substring(0,i) + "<span class='dict-italic'>" + match.substring(i)+"</span>";
            return result.replace("<</", "</");
        }

        function blackChinese(match) {
            var i = match.length;
            if (match[i-1] === ")" || match[i-1] === "]" || match[i-1] === "。" || match[i-1] === "】" || match[i-1] === "〉")
                return match;
            return  "<span class='dict-working-set-word'>" + match + "</span>";
        }
		
		function workingSetSlash(match) {
            return  "<span class='dict-working-set-slash'>" + match.trim() + "</span> ";
        }
		
        function processSquareBrackets(match) {
            /*console.log('processSquareBrackets: '  + match);*/
            var i = match.indexOf(']');
            /*console.log(match[0] + "<b><i>" + match.substring(1, i)+"</i></b>"+match[i]);*/
            return match[0] + "<b><i>" + match.substring(1, i)+"</i></b>"+match[i];
        }

        function parseTranslation() {
            originalTranslation = $('#translation').text().trim();
            var result = " " + originalTranslation;
            result = result.replace(new RegExp("　", "g"), " ");
            result = result.replace(new RegExp("\\u00A0", "g"), " ");
            result = result.replace(new RegExp("：", "g"), ":");
            result = result.replace(new RegExp("，", "g"), ",");
            result = result.replace(new RegExp("({|｛)", 'g'), "<br><span class='dict-curly-brackets'>$&").replace(new RegExp("(}|｝)", 'g'), "$&</span>");
            result = result.replace(new RegExp("(\\sI\\s|\\sII\\s|\\sIII\\s|\\sIV\\s|\\sV\\s|\\sVI\\s|\\sVII\\s|\\sVIII\\s|\\sIX\\s|\\sX\\s)", 'g'), "<br><span class='dict-roman-numbers'>$&</span> ");
            result = result.replace(new RegExp("(\\s名\\s|\\s动\\s|\\s形\\s|\\s副\\s|\\s代\\s|\\s量\\s|\\s数\\s|\\s介\\s|\\s连\\s|\\s助\\s|\\s叹\\s|\\s象\\s|\\s缀\\s|\\s素\\s|\\s音\\s)", 'g'), "<span class='dict-chinese-grammar'>$&</span> ");
            result = result.replace(new RegExp('\\s\\d+\\s', 'g'), "<br><span class='dict-numbers'>$&)</span> ");
            result = result.replace(new RegExp('\\s\\)', 'g'), ')');
            result = result.replace(new RegExp("(◊)", 'g'), "<span class='dict-romb'> $& </span>");
            result = result.replace(new RegExp("(〈褒〉|〈本〉|〈贬〉|〈插〉|〈成〉|〈粗〉|〈方〉|〈讽〉|〈古〉|〈惯〉|〈诙〉|〈讳〉|〈简〉|〈敬〉|〈旧〉|〈口〉|〈俚〉|〈昵〉|〈谦〉|〈书〉|〈熟〉|〈俗〉|〈套〉|〈通〉|〈婉〉|〈文〉|〈音译〉|〈喻〉|〈直〉|〈转〉|〈成,贬〉|〈口,诙〉|〈歇〉|〈别〉|〈成〉|〈本〉|〈常贬〉|〈多口〉|〈多喻〉|〈多书〉|〈多讽〉|〈多谦〉|〈多贬〉|〈多旧〉|〈多褒〉)", 'g'), "<span class='dict-angle-brackets'> $& </span>");
            result = result.replace(new RegExp("(【材】|【测】|【船】|【道】|【地理】|【地质】|【电】|【电子】|【动】|【法】|【纺】|【佛】|【工】|【工美】|【航】|【核】|【化】|【画】|【机】|【基】|【计】|【建】|【交】|【教】|【解】|【金】|【经】|【剧】|【军】|【考古】|【会】|【矿】|【昆】|【历】|【林】|【逻】|【美】|【民】|【鸟】|【农】|【烹】|【气】|【曲】|【商】|【摄】|【神】|【生化】|【生理】|【生态】|【生物】|【食】|【石化】|【石油】|【史】|【书法】|【数】|【水】|【体】|【天】|【统】|【外】|【网】|【微】|【文】|【武】|【舞】|【物】|【信】|【修】|【心】|【畜】|【药】|【冶】|【伊】|【医】|【印】|【影视】|【鱼】|【渔】|【语】|【园】|【乐】|【杂】|【哲】|【植】|【纸】|【制图】|【中医】|【中药】|【宗】|【棋】|【古生】|【化工】|【政】|【儒】|【无】)", 'g'), "<span class='dict-chinese-brackets'> $& </span>");
            result = result.replace(new RegExp("(იხ.\\sსურ.\\s|იხ.სურ.\\s|იხ.\\sანტ.\\s|იხ.ანტ.\\s|იხ.\\s|იგივეა,\\sრაც\\s|იგივეა\\sრაც\\s|ასევე\\s)(.*?)(【|〈|\\(|\\)|\\[|\\]|[\u10A0-\u10FF]|<|;|$)", 'g'), generateLink);
            result = result.replace(new RegExp("(([\u0000-\u007F]|[\u00C0-\u00F0]|[\u10A0-\u10FF]|[>):])(\\s|:\\s|.\\s|\\):\\s|]:\\s|“)(<span\\sclass='dict-chinese-brackets|[\u300A]|[\u3400-\u4DBF]|[\u4E00-\u62FF]|[\u6300-\u77FF]|[\u7800-\u8CFF]|[\u8D00-\u9FFF]))+", 'g'), insertBreak);
            result = result.replace(new RegExp("(“)+", 'g'), insertBreak);
            result = result.replace(new RegExp("(\\)|〉|〉\\s|】|】\\s|\\s|\\s名\\s|\\s动\\s|\\s形\\s|\\s副\\s|\\s代\\s|\\s量\\s|\\s数\\s|\\s介\\s|\\s连\\s|\\s助\\s|\\s叹\\s|\\s象\\s|\\s缀\\s|\\s素\\s|\\s音\\s)(</span>)(.*?)(:\\.|:|<br>)+", 'g'), setBold);
            result = result.replace(new RegExp('(\\[)([^:]*)(\\])', 'g'), processSquareBrackets);
            result = result.replace(new RegExp("(იხ\\.\\sსურ\\.\\s|იხ\\.სურ\\.\\s|იხ\\.\\sანტ\\.\\s|იხ\\.ანტ\\.\\s|გადატ\\.|ჩვეულ\\.\\s|მაგ\\.\\s|და\\sმისთ\\.|და\\sა\\.შ\\.|ითქმ.\\s|ინგ\\.\\s|ფრან\\.\\s|ესპ\\.\\s|რუს\\.\\s|გერმ\\.\\s|სანსკრ\\.\\s|ბერძ\\.\\s|ლათ\\.\\s|არაბ\\.\\s|იტალ\\.\\s|ებრ\\.\\s|ივრით\\.\\s)", 'g'), setItalic);
            result = result.replace(new RegExp("(იხ\\.\\s|იგივეა,\\sრაც|იგივეა\\sრაც)(.*?)(【|〈|\\(|\\)|\\[|\\]|[\u10A0-\u10FF]|<|;|$)", 'g'), setItalic);
            result = result.replace(new RegExp("(ასევე\\s)(【|〈|\\(|\\)|\\[|\\]|[\u300A]|[\u3400-\u4DBF]|[\u4E00-\u62FF]|[\u6300-\u77FF]|[\u7800-\u8CFF]|[\u8D00-\u9FFF]|<|;|$)", 'g'), setItalic);
            result = result.replace(new RegExp('გვარი\\s-', 'g'), "<b>$&</b>");
            result = result.replace(new RegExp(":\\.", "g"), "");
            result = result.replace(new RegExp("_", "g"), "");
            result = result.replace(new RegExp("^ "), '');
            result = result.replace(new RegExp("丶", "g"), '');
            result = result.replace(new RegExp("^<br>"), '');
            $('#translation').html(result);
        }

        function parseWorkingSet() {
            originalWorkingSet = $('#workingSet').text().trim();
            var result = originalWorkingSet.replace(new RegExp("＊", "g"), '*');
            var pos = result.indexOf('**');
            if (pos === -1) {
                result = originalWorkingSet;
            } else {
                result = result.replace('**', "</span>**");
                result = result.replace(new RegExp("(\\d+|\\sVIII|\\sVII|\\sVI|\\sIX|\\sIV|\\sIII|\\sII|\\sI|\\sV|\\sX)", "g"), "<span class='dict-numbers-back'>$&</span>");
                result = "<span class='dict-working-set-1'>" + result;
                result = result.replace('***', "***<span class='dict-working-set-3'>");
                result = result.replace('****', "</span>****<span class='dict-working-set-4'>");
                result = result.replace('*****', "</span>*****<span class='dict-working-set-5'>");
                result = result + "</span>";
				
                /*result = result.replace(new RegExp("。", 'g'), ".");*/
                result = result.replace(new RegExp("(([\u3400-\u4DBF\u4E00-\u62FF\u6300-\u77FF\u7800-\u8CFF\u8D00-\u9FFF])(\\)|】|〉|]|))+", 'g'), blackChinese);
            }
			result = result.replace(new RegExp("(///\\s|//\\s)+", 'g'), workingSetSlash);
            $('#workingSet').html(result);
        }

        function parseComment() {
            originalComment = $('#comment').text().trim();
            var result = originalComment.replace(new RegExp("(#)(.*?)(#)", "g"), "<span class='dict-comment'>$&</span>");
            result = result.replace(new RegExp("(&)(.*?)(&)", "g"), "<span class='dict-comment-1'>$&</span>");
            $('#comment').html(result);
        }

