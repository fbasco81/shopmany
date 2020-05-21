package pay;

import com.rabbitmq.client.*;
import io.jaegertracing.Configuration;
import io.opentracing.Scope;
import io.opentracing.Span;
import io.opentracing.SpanContext;
import io.opentracing.Tracer;
import io.opentracing.contrib.rabbitmq.TracingChannel;
import io.opentracing.contrib.rabbitmq.TracingConnectionFactory;
import io.opentracing.propagation.Format;
import io.opentracing.propagation.TextMapExtractAdapter;
import io.opentracing.propagation.TextMapInjectAdapter;
import io.opentracing.tag.Tags;
import io.opentracing.util.GlobalTracer;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import javax.servlet.http.HttpServletResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import com.google.gson.Gson;
import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.TimeoutException;

@SpringBootApplication
@RestController
public class Application {
    private static final Logger logger = LoggerFactory.getLogger(Application.class);
    private final static String QUEUE_NAME = "payment";

    private PayRepository payRepository;

    public Application(PayRepository payRepository) {
        this.payRepository = payRepository;
    }

    @GetMapping("/pays")
    public ResponseEntity<?>  home() {
        return ResponseEntity.ok().body(payRepository.findAll());
    }

    @PostMapping("/pay")
    public ResponseEntity<?> home2(@RequestHeader Map<String, String> headers, @RequestBody PayRequest payRequest) throws IOException {
        PayEntity payEntity = new PayEntity(payRequest.getTot_price(), payRequest.getCustomer_id());
        payRepository.save(payEntity);

        logger.info("PAY: entity saved");

        ConnectionFactory factory = new ConnectionFactory();

        logger.info("PAY: host:" + System. getenv("RABBIT_HOST"));

        factory.setHost(System. getenv("RABBIT_HOST"));

        try (Connection connection = factory.newConnection();
             Channel channel = connection.createChannel()) {

            logger.info("PAY: connection opened to host " + System. getenv("RABBIT_HOST"));

            channel.queueDeclare(QUEUE_NAME, false, false, false, null);

            // create message
            PayMessage payMessage = new PayMessage();
            payMessage.setCustomerId(payRequest.getCustomer_id());
            payMessage.setStatus("Confirmed");

            // convert to JSON
            Gson gson = new Gson();
            String body = gson.toJson(payMessage);

            // Instantiate tracer
            Tracer tracer = Configuration.fromEnv().getTracer();

// Optionally register tracer with GlobalTracer
           // GlobalTracer.register(tracer);

// Decorate RabbitMQ Channel with TracingChannel
            TracingChannel tracingChannel = new TracingChannel(channel, tracer);

            Tracer.SpanBuilder spanBuilder;
            try {
                SpanContext parentSpan = tracer.extract(Format.Builtin.HTTP_HEADERS, new TextMapExtractAdapter(headers));
                if (parentSpan == null) {
                    spanBuilder = tracer.buildSpan("pay");
                    logger.info("PAY: parent span null");
                } else {
                    spanBuilder = tracer.buildSpan("pay").asChildOf(parentSpan);
                    logger.info("PAY: parent span:" + gson.toJson(parentSpan));
                }
            } catch (IllegalArgumentException e) {
                spanBuilder = tracer.buildSpan("pay");
            }
            Scope scope = spanBuilder.withTag(Tags.SPAN_KIND.getKey(), Tags.SPAN_KIND_SERVER).startActive(true);
            Map<String, String> dictionary = new HashMap<String, String>();
            tracer.inject(scope.span().context(), Format.Builtin.TEXT_MAP, new TextMapInjectAdapter(dictionary));


         /*   Scope scope = tracer.buildSpan("item-sold").asChildOf().startActive(true);
            Span span = scope.span().setTag(String.valueOf(Tags.SPAN_KIND), Tags.SPAN_KIND_CLIENT);
            Map<String, String> dictionary = new HashMap<String, String>();
            tracer.inject(span.context(), Format.Builtin.TEXT_MAP, new TextMapInjectAdapter(dictionary));
            span.finish();
          */

            logger.info("PAY: dictionary message header:" + gson.toJson(dictionary));

            // Build message properties with tracing keys
            Map messageProps = new HashMap();
            messageProps.put("tracingKeys", dictionary);

            AMQP.BasicProperties.Builder basicProperties = new AMQP.BasicProperties.Builder();
            basicProperties.headers(messageProps);

            tracingChannel.basicPublish("", QUEUE_NAME, basicProperties.build(), body.getBytes());

            logger.info("PAY: message sent");

        } catch (TimeoutException e) {
            logger.info("PAY: exception: " + e.toString() );

            e.printStackTrace();
        }

        return ResponseEntity.ok("Success");
    }

    @GetMapping("/health")
    @ResponseBody
    public HealthResponse health(HttpServletResponse response) {
        HealthResponse h = new HealthResponse();
        String status = "unhealthy";

        HealthCheck mysqlC = new HealthCheck();
        mysqlC.setName("mysql");
        try {
            payRepository.count();
            status = "healthy";
            mysqlC.setStatus("healthy");
        } catch (Exception e) {
            logger.error("Mysql healthcheck failed", e.getMessage());
            mysqlC.setStatus("unhealthy");
            mysqlC.setError(e.getMessage());
            response.setStatus(500);
        }
        h.setStatus(status);
        h.addHealthCheck(mysqlC);
        return h;
    }

    public static void main(String[] args) {
        SpringApplication.run(Application.class, args);
    }

}
