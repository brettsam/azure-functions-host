module.exports = function (context, timerInfo) {
    context.log(context.bindings.input);
    // never call context.done();
}