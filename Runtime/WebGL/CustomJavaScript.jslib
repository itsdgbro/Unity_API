mergeInto(LibraryManager.library, {
    GetIframeData: function () {
        var iframe = window.parent.document.getElementById('cvgame');
        if (iframe) {
            var dataGame = iframe.getAttribute('data-game');
            if (dataGame) {
                var lengthBytes = lengthBytesUTF8(dataGame) + 1;
                var stringOnHeap = _malloc(lengthBytes);
                stringToUTF8(dataGame, stringOnHeap, lengthBytes);
                return stringOnHeap;
            } else {
                // Log to browser console if data-game is missing
                console.error("Missing 'data-game' attribute.");
                return 0; // Null pointer if no data-game attribute is found
            }
        } else {
            // Log to browser console if iframe is not found
            console.error("Iframe with id 'cvgame' not found in the DOM.");
            return 0; // Null pointer if iframe is not found
        }
    }
});
