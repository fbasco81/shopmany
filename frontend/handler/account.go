package handler

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net/http"

	"github.com/gianarb/shopmany/frontend/config"
	// "go.opentelemetry.io/otel/api/propagation"
	// "go.opentelemetry.io/otel/plugin/httptrace"

	opentracing "github.com/opentracing/opentracing-go"

	"go.uber.org/zap"
)

type AccountResponse struct {
	Id       string `json:"id"`
	Name     string `json:"name"`
	Discount int    `json:"fideltyPoint"`
}

type accountHandler struct {
	config  config.Config
	hclient *http.Client
	logger  *zap.Logger
}

func NewAccountHandler(config config.Config, hclient *http.Client) *accountHandler {
	logger, _ := zap.NewProduction()
	return &accountHandler{
		config:  config,
		hclient: hclient,
		logger:  logger,
	}
}

func (h *accountHandler) WithLogger(logger *zap.Logger) {
	h.logger = logger
}

func (h *accountHandler) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	w.Header().Add("Content-Type", "application/json")
	// if r.Method != "POST" {
	// 	http.Error(w, "Method not supported", 405)
	// 	return
	// }
	req, err := http.NewRequest("GET", fmt.Sprintf("%s/account", h.config.AccountHost), nil)
	if err != nil {
		http.Error(w, err.Error(), 500)
		return
	}
	// ctx, req := httptrace.W3C(r.Context(), req)
	// propagation.InjectHTTP(ctx, props, req.Header)

	ctx := r.Context()
	req.WithContext(ctx)
	if span := opentracing.SpanFromContext(r.Context()); span != nil {
		opentracing.GlobalTracer().Inject(
			span.Context(),
			opentracing.HTTPHeaders,
			opentracing.HTTPHeadersCarrier(req.Header))
	}

	//req.Header.Add("Content-Type", "application/json")
	resp, err := h.hclient.Do(req)
	if err != nil {
		h.logger.Error(err.Error())
		http.Error(w, err.Error(), 500)
		return
	}
	defer resp.Body.Close()

	body, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		h.logger.Error(err.Error())
		http.Error(w, err.Error(), 500)
		return
	}
	fmt.Printf("Body from account: %s\n", string(body))

	account := AccountResponse{}
	err = json.Unmarshal(body, &account)
	if err != nil {
		h.logger.Error(err.Error())
		http.Error(w, err.Error(), 500)
		return
	}

	// log response content
	b, err := json.Marshal(account)
	fmt.Fprintf(w, string(b))
}
