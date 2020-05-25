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

import javax.servlet.http.HttpServletRequest;
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
    public ResponseEntity<?> home2(@RequestHeader Map<String, String> headers, @RequestBody PayRequest payRequest, @RequestAttribute("span") Span requestSpan) throws IOException {
        PayEntity payEntity = new PayEntity(payRequest.getTot_price(), payRequest.getCustomer_id());

        // save payment
        payRepository.save(payEntity);
        logger.info("PAY: entity saved");

        // prepare connection to rabbit
        ConnectionFactory factory = new ConnectionFactory();
        factory.setHost(System. getenv("RABBIT_HOST"));
        logger.info("PAY: host:" + System. getenv("RABBIT_HOST"));

        // open connection
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

            // Decorate RabbitMQ Channel with TracingChannel
            TracingChannel tracingChannel = new TracingChannel(channel, tracer);

            // create span as child of the one in the current request
            Tracer.SpanBuilder spanBuilder = tracer.buildSpan("pay").asChildOf(requestSpan);;
            Span childSpan = spanBuilder.withTag(Tags.SPAN_KIND.getKey(), Tags.SPAN_KIND_SERVER).start();
            Map<String, String> dictionary = new HashMap<String, String>();
            tracer.inject(childSpan.context(), Format.Builtin.TEXT_MAP, new TextMapInjectAdapter(dictionary));

            // Build message properties with tracing keys
            Map messageProps = new HashMap();
            messageProps.put("tracingKeys", dictionary);
            AMQP.BasicProperties.Builder basicProperties = new AMQP.BasicProperties.Builder();
            basicProperties.headers(messageProps);

            // send message
            tracingChannel.basicPublish("", QUEUE_NAME, basicProperties.build(), body.getBytes());

            // complete span
            logger.info("PAY: message sent");
            childSpan.finish();

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
