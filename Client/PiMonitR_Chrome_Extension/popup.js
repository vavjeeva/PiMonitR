var __awaiter = chrome.extension.getBackgroundPage().__awaiter;
var connection = chrome.extension.getBackgroundPage().connection;

document.addEventListener('DOMContentLoaded', function () {
    const streamStartButton = document.getElementById('streamStartButton');
    const streamStopButton = document.getElementById('streamStopButton');
    const streamContent = document.getElementById('streamContent');
    const logContent = document.getElementById('logContent');

    streamStartButton.addEventListener("click", (event) => __awaiter(this, void 0, void 0, function* () {
        streamStartButton.setAttribute("disabled", "disabled");
        streamStopButton.removeAttribute("disabled");
        try {
            connection.stream("StartStream")
                .subscribe({
                    next: (item) => {                      
                        streamContent.src = "data:image/jpg;base64," + item;                       
                    },
                    complete: () => {
                        var li = document.createElement("li");
                        li.textContent = "Stream completed";
                        logContent.appendChild(li);
                    },
                    error: (err) => {
                        var li = document.createElement("li");
                        li.textContent = err;
                        logContent.appendChild(li);
                    },
                });
        }
        catch (e) {
            console.error(e.toString());
        }
        event.preventDefault();
    }));

    streamStopButton.addEventListener("click", function () {
        streamStopButton.setAttribute("disabled", "disabled");
        streamStartButton.removeAttribute("disabled");
        connection.invoke("StopStream").catch(err => console.error(err.toString()));
        event.preventDefault();
    });

    connection.on("StopStream", () => {
        var li = document.createElement("li");
        li.textContent = "stream closed";
        logContent.appendChild(li);        
        streamStopButton.setAttribute("disabled", "disabled");
        streamStartButton.removeAttribute("disabled");
    });
});